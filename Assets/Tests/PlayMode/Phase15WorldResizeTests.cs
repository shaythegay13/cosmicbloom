// P3-Phase 15 verification — the structural region resize (20x20 → 40x40):
//  * tier rings scale proportionally (wild 0..5, mid 6..11, lush core 12..27)
//  * the lush core is 16x16 = 256 cells (tier-2 quality zone; since P3-Phase 17
//    FarmGrid populates plot views on demand, not eagerly — see that test)
//  * regions sit 80 apart (one region-width travel gap preserved)
//  * SaveMigration shifts pre-resize plot keys +6/+6 exactly once, drops the
//    stored player position, and stamps worldVersion so it can't run twice
using System.Collections;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.Save;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class Phase15WorldResizeTests
    {
        [Test]
        public void TierRings_ScaleProportionallyTo40x40()
        {
            Assert.AreEqual(40, RegionWorldBuilder.Size, "2x linear resize");
            Assert.AreEqual(80, RegionWorldBuilder.Spacing, "spacing keeps a one-region gap");
            Assert.AreEqual(80f, RegionWorldBuilder.OriginOf(1).x, "second region origin");

            // ring boundaries along the x axis at mid-height (edge distance = x)
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(5, 20), "outer wild ring up to edge 5");
            Assert.AreEqual(1, RegionWorldBuilder.TierAt(6, 20), "mid ring starts at edge 6");
            Assert.AreEqual(1, RegionWorldBuilder.TierAt(11, 20), "mid ring up to edge 11");
            Assert.AreEqual(2, RegionWorldBuilder.TierAt(12, 20), "lush core starts at edge 12");
            Assert.AreEqual(2, RegionWorldBuilder.TierAt(27, 27), "lush core far corner");
            Assert.AreEqual(1, RegionWorldBuilder.TierAt(28, 20), "east of the core is mid ring again");

            int farmable = 0;
            for (int y = 0; y < RegionWorldBuilder.Size; y++)
                for (int x = 0; x < RegionWorldBuilder.Size; x++)
                    if (RegionWorldBuilder.IsFarmable(x, y)) farmable++;
            Assert.AreEqual(256, farmable, "16x16 lush core = 256 farmable plots per region");
        }

        // P3-Phase 17: FarmGrid no longer eagerly instantiates the 256-cell core.
        // Views exist only for plots that actually exist — those restored from the
        // save on load, and new ones the moment a cell is tilled (on demand).
        [UnityTest]
        public IEnumerator FarmGrid_CreatesViewsOnDemand_NotEagerly()
        {
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            var farming = new FarmingSystem(state);

            var fg = FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);
            yield return null;
            Assert.AreEqual(0, fg.transform.childCount, "empty save → no eager plot views (was 256)");

            // tilling a cell materialises exactly one view, wherever it is — a wild
            // outer-ring cell far from the old lush-core footprint
            Assert.IsTrue(farming.Till(3, 3), "till a wild-tier cell");
            yield return null;
            Assert.AreEqual(1, fg.transform.childCount, "one view created on demand");
            Assert.IsNotNull(GameObject.Find("Plot_3_3"), "the tilled wild cell has a view");
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(3, 3), "(3,3) is wild tier — outside the old 256 core");

            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }

        [Test]
        public void SaveMigration_ShiftsPlotKeysOnce_AndDropsStalePlayerPos()
        {
            var save = new SaveSnapshot();
            var vale = new FarmState();
            vale.plots["10,10"] = new PlotState { tilled = true, soilHealth = 42f };
            vale.plots["6,13"] = new PlotState
            {
                tilled = true,
                crop = new CropState { id = "starlight_berry", stage = 1 },
            };
            save.farms["starlight_vale"] = vale;
            save.playerX = 12.25f;
            save.playerY = 8.75f;
            save.hasPlayerPos = true;

            Assert.IsTrue(SaveMigration.Apply(save), "pre-resize save migrates");

            Assert.IsFalse(vale.plots.ContainsKey("10,10"), "old key removed");
            Assert.IsTrue(vale.plots.ContainsKey("16,16"), "key shifted by +6/+6");
            Assert.AreEqual(42f, vale.plots["16,16"].soilHealth, 0.001f, "plot state carried over");
            Assert.AreEqual("starlight_berry", vale.plots["12,19"].crop.id, "crop survives the shift");
            Assert.IsTrue(RegionWorldBuilder.IsFarmable(16, 16), "migrated cell is in the new core");
            Assert.IsTrue(RegionWorldBuilder.IsFarmable(12, 19), "old core maps entirely into the new core");

            Assert.IsFalse(save.hasPlayerPos, "old-world player position dropped (spawn at region center)");
            Assert.AreEqual(SaveMigration.CurrentWorldVersion, save.worldVersion, "worldVersion stamped");
            Assert.AreEqual(2, save.schema, "server schema contract untouched");

            Assert.IsFalse(SaveMigration.Apply(save), "already-migrated save is left alone");
            Assert.IsTrue(vale.plots.ContainsKey("16,16"), "second Apply does not re-shift keys");
            Assert.IsFalse(vale.plots.ContainsKey("22,22"), "second Apply does not re-shift keys");
        }

        [Test]
        public void SaveMigration_FreshSnapshotIsHarmless()
        {
            var save = new SaveSnapshot();
            Assert.IsTrue(SaveMigration.Apply(save), "fresh snapshot gets stamped");
            Assert.AreEqual(0, save.farms.Count, "nothing to migrate");
            Assert.IsFalse(save.hasPlayerPos);
            Assert.AreEqual(SaveMigration.CurrentWorldVersion, save.worldVersion);
        }
    }
}
