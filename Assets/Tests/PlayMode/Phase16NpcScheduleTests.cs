// P3-Phase 16 verification — NPC daily schedules. TimeManager's new 4-period
// day drives NPCs between code-side schedule stops (NpcSchedule): the
// transform-position test proves an NPC actually WALKS to a new stop when the
// period changes (not assumed behavior), and the screenshot test captures the
// SAME NPC at two different stops at two different times of day
// (Logs/phase16_npc_*.png). The screenshot test renders a camera, so it
// belongs in the RENDER split, never the -nographics filter.
using System.Collections;
using System.IO;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class Phase16NpcScheduleTests
    {
        TimeManager _time;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _time = new GameObject("TestTime").AddComponent<TimeManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_time.gameObject);
            GameEvents.Clear();
        }

        [Test]
        public void PeriodBoundaries_DeriveFromDayNightWindow()
        {
            // Day span [0.25, 0.78) splits into thirds at ~0.4267 and ~0.6033.
            foreach (var (t, expected) in new[]
            {
                (0.26f, TimeManager.DayPeriod.Morning),
                (0.42f, TimeManager.DayPeriod.Morning),
                (0.43f, TimeManager.DayPeriod.Midday),
                (0.60f, TimeManager.DayPeriod.Midday),
                (0.61f, TimeManager.DayPeriod.Evening),
                (0.77f, TimeManager.DayPeriod.Evening),
                (0.78f, TimeManager.DayPeriod.Night),
                (0.95f, TimeManager.DayPeriod.Night),
                (0.10f, TimeManager.DayPeriod.Night),
            })
            {
                _time.timeOfDay = t;
                Assert.AreEqual(expected, _time.CurrentPeriod, $"period at t={t}");
                // Periods must never disagree with the existing day/night binary.
                Assert.AreEqual(_time.IsNight, _time.CurrentPeriod == TimeManager.DayPeriod.Night,
                    $"period/binary consistency at t={t}");
            }
        }

        [Test]
        public void StopMapping_RespectsActiveAtWindows()
        {
            // Day folk: fields in the morning, plaza at midday, home after.
            Assert.AreEqual(NpcSchedule.Stop.Work, NpcSchedule.StopFor("day", TimeManager.DayPeriod.Morning));
            Assert.AreEqual(NpcSchedule.Stop.Social, NpcSchedule.StopFor("day", TimeManager.DayPeriod.Midday));
            Assert.AreEqual(NpcSchedule.Stop.Home, NpcSchedule.StopFor("day", TimeManager.DayPeriod.Evening));
            Assert.AreEqual(NpcSchedule.Stop.Home, NpcSchedule.StopFor("day", TimeManager.DayPeriod.Night));
            // Night folk mirror into their waking window.
            Assert.AreEqual(NpcSchedule.Stop.Home, NpcSchedule.StopFor("night", TimeManager.DayPeriod.Midday));
            Assert.AreEqual(NpcSchedule.Stop.Work, NpcSchedule.StopFor("night", TimeManager.DayPeriod.Evening));
            Assert.AreEqual(NpcSchedule.Stop.Social, NpcSchedule.StopFor("night", TimeManager.DayPeriod.Night));
            // "any" folk take a second work shift and sleep at night.
            Assert.AreEqual(NpcSchedule.Stop.Work, NpcSchedule.StopFor("any", TimeManager.DayPeriod.Evening));
            Assert.AreEqual(NpcSchedule.Stop.Home, NpcSchedule.StopFor("any", TimeManager.DayPeriod.Night));

            // Every activeAt value in the actual data has 2+ distinct stops per day.
            foreach (var activeAt in GameDatabase.Npcs.Select(n => n.activeAt).Distinct())
            {
                var stops = System.Enum.GetValues(typeof(TimeManager.DayPeriod))
                    .Cast<TimeManager.DayPeriod>()
                    .Select(p => NpcSchedule.StopFor(activeAt, p))
                    .Distinct().Count();
                Assert.GreaterOrEqual(stops, 2, $"activeAt='{activeAt}' should visit 2+ stops");
            }
        }

        [Test]
        public void SocialStop_ScattersPerNpc_NoStacking()
        {
            // Live-playtest regression (2026-07-11): a full 10-NPC roster sharing
            // one exact plaza point rendered as a single NPC — "they all moved
            // off the map". Every roster slot must get its own plaza spot.
            var center = new Vector3(20f, 20f, 0f);
            var positions = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < 10; i++)   // NpcController.Spawn's ring-angle formula, 10 per region
            {
                float angle = (Mathf.PI * 2f * i) / 10f + 0.4f;
                positions.Add(NpcSchedule.StopPosition(NpcSchedule.Stop.Social, center, angle));
            }

            var plaza = center + NpcSchedule.SocialOffset;
            for (int i = 0; i < positions.Count; i++)
            {
                Assert.LessOrEqual((positions[i] - plaza).magnitude, NpcSchedule.SocialScatter + 0.01f,
                    $"slot {i} stays gathered near the plaza");
                for (int j = i + 1; j < positions.Count; j++)
                    Assert.Greater((positions[i] - positions[j]).magnitude, 1f,
                        $"slots {i} and {j} must not stack on the same point");
            }
        }

        [UnityTest]
        public IEnumerator NpcWalksToNewStop_AtPeriodTransition()
        {
            var def = GameDatabase.Npcs.First(n => n.activeAt == "day");
            var center = new Vector3(20f, 20f, 0f);

            _time.timeOfDay = 0.30f;   // Morning
            Assert.AreEqual(TimeManager.DayPeriod.Morning, _time.CurrentPeriod);

            var npc = NpcController.Spawn(def, center, 0, 10);
            npc.walkSpeed = 200f;  // batchmode frames have tiny deltaTime; keep the walk short in frames
            yield return null;     // Start() subscribes to OnPeriodChanged

            float angle = 0.4f;    // index 0 of 10 → Spawn's ring angle
            var workPos = NpcSchedule.StopPosition(NpcSchedule.Stop.Work, center, angle);
            var socialPos = NpcSchedule.StopPosition(NpcSchedule.Stop.Social, center, angle);
            Assert.Less((npc.transform.position - workPos).magnitude, 0.05f,
                "morning spawn snaps to the Work stop");
            var morningPos = npc.transform.position;

            _time.timeOfDay = 0.55f;   // jump to Midday → OnPeriodChanged next Update
            for (int i = 0; i < 3000 && (npc.transform.position - socialPos).magnitude > 0.05f; i++)
                yield return null;

            Assert.Greater((npc.transform.position - morningPos).magnitude, 5f,
                "NPC transform actually moved away from the morning Work stop");
            Assert.Less((npc.transform.position - socialPos).magnitude, 0.05f,
                "NPC arrived at the Midday Social stop (shared plaza)");
            Debug.Log($"[P16Test] {def.id} walked Work {morningPos} → Social {npc.transform.position} at Midday ✓");

            Object.DestroyImmediate(npc.gameObject);
        }

        [UnityTest]
        public IEnumerator NpcScheduleScreenshots_SameNpcTwoPlacesTwoTimes()
        {
            // An activeAt=any NPC stays visible at night, so one NPC can be
            // photographed at Work (morning) and back Home (night).
            var def = GameDatabase.Npcs.First(n => n.activeAt == "any");
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            Assert.IsTrue(regions.TryGetBounds(def.region, out var bounds));
            var lighting = DayNightLighting.Create();
            yield return null;

            _time.timeOfDay = 0.30f;   // Morning
            var npc = NpcController.Spawn(def, bounds.center, 0, 10);
            npc.walkSpeed = 200f;
            yield return null;

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 16f;   // covers home ring r=14.4 and work r=9.5
            cam.transform.position = bounds.center + new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            DayNightLighting.Assign2DRenderer(cam);

            Vector3 morningPos = default, nightPos = default;
            foreach (var (t, name) in new[] { (0.30f, "morning_work"), (0.92f, "night_home") })
            {
                _time.timeOfDay = t;
                // let the period event fire, the NPC walk to its stop, and lighting settle
                for (int i = 0; i < 3000; i++)
                {
                    yield return null;
                    var target = NpcSchedule.StopPosition(
                        NpcSchedule.StopFor(def.activeAt, _time.CurrentPeriod), bounds.center, 0.4f);
                    if ((npc.transform.position - target).magnitude < 0.05f) break;
                }
                yield return null;
                if (name == "morning_work") morningPos = npc.transform.position;
                else nightPos = npc.transform.position;

                var rt = new RenderTexture(800, 800, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(800, 800, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", $"phase16_npc_{name}.png"), tex.EncodeToPNG());

                // where to look for the NPC in the PNG, for visual review
                var rel = (npc.transform.position - bounds.center) / (cam.orthographicSize * 2f);
                Debug.Log($"[P16Test] {def.id} at {name}: world {npc.transform.position}, " +
                          $"pixel ≈ ({400 + rel.x * 800:F0}, {400 + rel.y * 800:F0}) in Logs/phase16_npc_{name}.png");
            }

            Assert.Greater((morningPos - nightPos).magnitude, 3f,
                "same NPC stands in two clearly different places at the two times of day");

            Object.DestroyImmediate(npc.gameObject);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lighting.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }
    }
}
