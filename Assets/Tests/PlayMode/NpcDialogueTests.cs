// Phase 4 verification — all 50 NPCs spawn from data into their regions with
// palette tints; talking shows each NPC's own dialogue (intro first, idle
// after) across at least 2 regions / 3 NPCs; day/night presence honored.
using System.Collections;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class NpcDialogueTests
    {
        RegionManager _regions;
        System.Collections.Generic.List<NpcController> _npcs;
        PlayerController _player;
        Save.SaveManager _saveGo;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            NpcController.ResetMetFallback();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            RegionWorldBuilder.BuildAll(_regions);
            _npcs = NpcSpawner.SpawnAll(_regions);
            _player = PlayerController.Spawn(new Vector3(10, 10));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var n in _npcs.Where(n => n != null)) Object.DestroyImmediate(n.gameObject);
            Object.DestroyImmediate(_player.gameObject);
            foreach (var def in GameDatabase.Regions)
            {
                var go = GameObject.Find(RegionWorldBuilder.ToPascal(def.id));
                if (go != null) Object.DestroyImmediate(go);
            }
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        [Test]
        public void AllFiftyNpcsSpawnIntoTheirRegions()
        {
            Assert.AreEqual(GameDatabase.Npcs.Count, _npcs.Count, "one controller per data NPC");
            Assert.AreEqual(50, _npcs.Count);

            foreach (var npc in _npcs)
            {
                Assert.IsTrue(_regions.TryGetBounds(npc.Def.region, out var b));
                var grown = b; grown.Expand(2f);   // ring placement may skim the edge
                Assert.IsTrue(grown.Contains(npc.transform.position),
                    $"{npc.Def.id} should stand in/near {npc.Def.region}, was {npc.transform.position}");
            }

            int perRegion = _npcs.Count(n => n.Def.region == "moon_garden");
            Assert.AreEqual(10, perRegion, "10 per region per data");
            Debug.Log($"[P4Test] 50 NPCs spawned; sample: " + string.Join(", ",
                _npcs.Take(5).Select(n => $"{n.Def.id}@{n.Def.region}")));
        }

        [UnityTest]
        public IEnumerator ThreeNpcsAcrossTwoRegions_ShowOwnDialogue()
        {
            string[] ids = { "zyx", "lumina" };   // starlight_vale pair
            var third = _npcs.First(n => n.Def.region == "sun_garden");

            foreach (var id in ids)
            {
                var npc = _npcs.First(n => n.Def.id == id);
                yield return TalkAndAssert(npc, npc.Def.dialogue["intro"]);
            }
            yield return TalkAndAssert(third, third.Def.dialogue["intro"]);

            // second chat with zyx shows idle, not intro (met-tracking)
            var zyx = _npcs.First(n => n.Def.id == "zyx");
            yield return TalkAndAssert(zyx, zyx.Def.dialogue["idle"]);
        }

        IEnumerator TalkAndAssert(NpcController npc, string expectedLine)
        {
            string talked = null;
            System.Action<string> capture = id => talked = id;
            GameEvents.OnTalkedTo += capture;

            _player.transform.position = npc.transform.position + new Vector3(0.7f, 0f);
            npc.Interact(_player);
            Assert.IsTrue(UI.DialogueUI.IsOpen, $"{npc.Def.id} opens dialogue");
            Assert.AreEqual(expectedLine, UI.DialogueUI.CurrentLine,
                $"{npc.Def.id} speaks its own data line");
            UI.DialogueUI.Close();
            Assert.AreEqual(npc.Def.id, talked, "talk event fired for quests");

            GameEvents.OnTalkedTo -= capture;
            Debug.Log($"[P4Test] {npc.Def.id} said: \"{expectedLine.Substring(0, Mathf.Min(60, expectedLine.Length))}…\"");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NightOnlyNpcsHideDuringDay()
        {
            var time = new GameObject("TestTime").AddComponent<TimeManager>();
            time.timeOfDay = 0.5f;   // noon
            yield return null;

            var nightNpc = _npcs.FirstOrDefault(n => n.Def.activeAt == "night");
            var dayNpc = _npcs.FirstOrDefault(n => n.Def.activeAt == "day");
            Assert.IsNotNull(nightNpc, "data has night NPCs");

            nightNpc.ApplyTimeOfDay();
            dayNpc.ApplyTimeOfDay();
            Assert.IsFalse(nightNpc.GetComponentInChildren<Renderer>(true).enabled,
                $"{nightNpc.Def.id} (night) hidden at noon");
            Assert.IsTrue(dayNpc.GetComponentInChildren<Renderer>(true).enabled,
                $"{dayNpc.Def.id} (day) visible at noon");

            time.timeOfDay = 0.9f;   // deep night
            nightNpc.ApplyTimeOfDay();
            dayNpc.ApplyTimeOfDay();
            Assert.IsTrue(nightNpc.GetComponentInChildren<Renderer>(true).enabled,
                $"{nightNpc.Def.id} visible at night");
            Assert.IsFalse(dayNpc.GetComponentInChildren<Renderer>(true).enabled,
                $"{dayNpc.Def.id} asleep at night");

            Object.DestroyImmediate(time.gameObject);
        }
    }
}
