// Cosmic Bloom — which crop a "plant" tap uses, per region. Defaults to the
// region's first native crop; UI cycling arrives with the HUD (Phase 9).
//
// DECISION (overnight run): quests award no seed items and there is no shop
// system in scope, so seeds are coin-purchased on demand at the crop's
// data-driven seedPrice when planting with an empty pouch. All crops sell
// above seed cost except clover_cover (a deliberate soil investment) and
// heartseed (quest-only, 0/0 prices — excluded from selection and purchase;
// it enters the pouch only as a quest reward item).
// New saves also get 5 starter Starlight Berry seeds (the data's tutorial crop).
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;

namespace CosmicBloom.World
{
    public static class CropSelection
    {
        static readonly Dictionary<string, string> _selected = new();   // regionId -> cropId

        /// <summary>Pure query — safe for UI labels; never spends coins.</summary>
        public static string SelectedFor(GameState state, string regionId)
        {
            if (!_selected.TryGetValue(regionId, out var cropId) || GameDatabase.Crop(cropId) == null)
            {
                cropId = OptionsFor(regionId).FirstOrDefault()?.id;
                if (cropId != null) _selected[regionId] = cropId;
            }
            return cropId;
        }

        /// <summary>Planting path: same selection, but tops up the pouch (may spend coins).</summary>
        public static string SelectedForPlanting(GameState state, string regionId)
        {
            var cropId = SelectedFor(state, regionId);
            EnsureSeed(state, cropId);
            return cropId;
        }

        /// <summary>Quest-only crops (heartseed: 0 seed / 0 sell) never appear in the picker.</summary>
        static bool Purchasable(CropDef def) => def.seedPrice > 0 || def.sellPrice > 0;

        public static void Select(string regionId, string cropId)
        {
            var def = GameDatabase.Crop(cropId);
            if (def != null && def.region == regionId) _selected[regionId] = cropId;
        }

        public static IEnumerable<CropDef> OptionsFor(string regionId) =>
            GameDatabase.CropsFor(regionId).Where(Purchasable);

        /// <summary>Market stand-in: buy one seed with coins at data seedPrice if pouch is empty.</summary>
        static void EnsureSeed(GameState state, string cropId)
        {
            if (cropId == null || state == null) return;
            if (state.Save.seeds.TryGetValue(cropId, out var n) && n > 0) return;

            var def = GameDatabase.Crop(cropId);
            if (def == null || !Purchasable(def)) return;
            if (state.SpendCoins(def.seedPrice))
                state.Save.seeds[cropId] = 1;
        }

        /// <summary>Starter pack for brand-new saves (no shop UI in this pass).</summary>
        public static void GrantStarterSeeds(GameState state)
        {
            if (state.Save.seeds.Count > 0 || state.Save.coins > 0 || state.Save.xp > 0) return;
            state.Save.seeds["starlight_berry"] = 5;
        }
    }
}
