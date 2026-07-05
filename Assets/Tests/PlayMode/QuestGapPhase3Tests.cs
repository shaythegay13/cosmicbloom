// Phase-2 Spec / Phase 3 verification — the two collapsed-Phase-3 builds from
// the user-approved rulings (QUEST_GAP_REPORT.md / CLAUDE.md):
//   q_comet_chaser    — witness_event counts firings of the SAME meteor-shower
//                       event q_meteor_mystery's wait_for uses (no new events).
//   q_festival_circuit — host_festival simplified per q_festival's own data
//                       precedent: 4 distinct NPCs of one Sacred Garden within
//                       a single in-game day = one festival; 4 gardens complete.
// Mirrors QuestGapPhase2Tests' SetUp shape. No rendering — runs in the
// -nographics half of the split suite.
using System.Collections;
using CosmicBloom.Core;
using CosmicBloom.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class QuestGapPhase3Tests
    {
        GameState _state;
        QuestEngine _engine;
        TimeManager _time;

        static readonly string[] SunNpcs =
            { "solmara", "helia", "cinder", "dune", "flint", "sola", "gleam", "basker" };
        static readonly string[] MoonNpcs = { "selene", "noctua", "dusk", "pearl" };
        static readonly string[] StarNpcs = { "astra", "vega", "altair", "polaris" };
        static readonly string[] NebulaNpcs = { "nyx", "ion", "quasar", "vela" };

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _state = new GameState(new SaveSnapshot());
            _time = new GameObject("TestTime").AddComponent<TimeManager>();
            _engine = new GameObject("TestQuestEngine").AddComponent<QuestEngine>();
            _engine.Bind(_state);
            UI.DialogueUI.Close();
        }

        [TearDown]
        public void TearDown()
        {
            if (MeteorShowerService.Instance != null)
                Object.DestroyImmediate(MeteorShowerService.Instance.gameObject);
            Object.DestroyImmediate(_engine.gameObject);
            Object.DestroyImmediate(_time.gameObject);
            GameEvents.Clear();
        }

        void TalkTo(params string[] npcIds)
        {
            foreach (var id in npcIds)
            {
                GameEvents.TalkedTo(id);
                UI.DialogueUI.Close();   // giver/side-quest chatter must not block later dialogue
            }
        }

        // ── q_comet_chaser: witness_event meteor_shower ×5 ────────────────────

        [Test]
        public void WitnessEvent_FiveMeteorShowers_CompleteQCometChaser()
        {
            _engine.StartQuest("q_comet_chaser");   // witness_event meteor_shower x5
            UI.DialogueUI.Close();
            Assert.IsFalse(QuestEngine.IsPendingSystemStep(_engine.CurrentStep("q_comet_chaser")),
                "witness_event no longer surfaces the skip-stub affordance");

            int coinsBefore = _state.Save.coins;
            for (int i = 0; i < 3; i++) GameEvents.MeteorShower();
            Assert.AreEqual(3, _engine.StepProgress("q_comet_chaser"), "each firing counts once");
            Assert.IsFalse(_engine.IsCompleted("q_comet_chaser"), "3 of 5 is not done");

            for (int i = 0; i < 2; i++) GameEvents.MeteorShower();
            Assert.IsTrue(_engine.IsCompleted("q_comet_chaser"), "5 witnessed showers complete the quest");
            Assert.AreEqual(coinsBefore + 1400, _state.Save.coins, "reward coins granted");
            Debug.Log("[P3Test] q_comet_chaser completed via 5 meteor-shower firings");
        }

        [UnityTest]
        public IEnumerator WitnessEvent_RealNightfalls_DriveTheCount()
        {
            // full pipeline: TimeManager.OnNightfall -> MeteorShowerService -> GameEvents -> QuestEngine
            MeteorShowerService.Create();
            _engine.StartQuest("q_comet_chaser");
            UI.DialogueUI.Close();

            for (int night = 0; night < 5; night++)
            {
                _time.timeOfDay = 0.5f;   // noon
                yield return null;
                _time.timeOfDay = 0.9f;   // past nightStart (0.78)
                yield return null;        // Update sees the day->night edge, fires OnNightfall
                UI.DialogueUI.Close();    // "A meteor shower streaks overhead!"
                Assert.AreEqual(Mathf.Min(night + 1, 5),
                    _engine.IsCompleted("q_comet_chaser") ? 5 : _engine.StepProgress("q_comet_chaser"),
                    $"nightfall #{night + 1} counted");
            }

            Assert.IsTrue(_engine.IsCompleted("q_comet_chaser"),
                "5 real nightfalls complete q_comet_chaser end-to-end");
            Debug.Log("[P3Test] q_comet_chaser completed via 5 real TimeManager nightfalls");
        }

        // ── q_festival_circuit: host_festival ×4 (simplified) ─────────────────

        [Test]
        public void HostFestival_FourGardensInOneDay_CompleteQFestivalCircuit()
        {
            _engine.StartQuest("q_festival_circuit");   // host_festival x4
            UI.DialogueUI.Close();
            Assert.IsFalse(QuestEngine.IsPendingSystemStep(_engine.CurrentStep("q_festival_circuit")),
                "host_festival no longer surfaces the skip-stub affordance");

            TalkTo(SunNpcs[0], SunNpcs[1], SunNpcs[2], SunNpcs[3]);
            Assert.AreEqual(1, _engine.StepProgress("q_festival_circuit"), "4 sun_garden NPCs = one festival");

            TalkTo(MoonNpcs);
            Assert.AreEqual(2, _engine.StepProgress("q_festival_circuit"));
            TalkTo(StarNpcs);
            Assert.AreEqual(3, _engine.StepProgress("q_festival_circuit"));
            TalkTo(NebulaNpcs);

            Assert.IsTrue(_engine.IsCompleted("q_festival_circuit"), "all four Sacred Gardens hosted");
            Assert.Contains("festival_master_banner", _state.Save.cosmeticsOwned, "reward item granted");
            Debug.Log("[P3Test] q_festival_circuit completed via 4 per-garden talk circuits");
        }

        [Test]
        public void HostFestival_SameGardenRepeats_DoNotDoubleCount()
        {
            _engine.StartQuest("q_festival_circuit");
            UI.DialogueUI.Close();

            TalkTo(SunNpcs);   // 8 distinct sun_garden NPCs, same day
            Assert.AreEqual(1, _engine.StepProgress("q_festival_circuit"),
                "extra NPCs in an already-hosted garden don't add festivals");

            TalkTo(SunNpcs[0], SunNpcs[1]);   // repeat visitors
            Assert.AreEqual(1, _engine.StepProgress("q_festival_circuit"), "repeat talks don't add festivals");
        }

        [Test]
        public void HostFestival_ValeNpcs_DoNotCount()
        {
            _engine.StartQuest("q_festival_circuit");
            UI.DialogueUI.Close();

            TalkTo("zyx", "lumina", "orbit", "elder_nova", "mira");   // starlight_vale (act 1)
            Assert.AreEqual(0, _engine.StepProgress("q_festival_circuit"),
                "festivals only happen in the four act-3 Sacred Gardens");
        }

        [Test]
        public void HostFestival_DayRollover_RestartsPartialTally()
        {
            _engine.StartQuest("q_festival_circuit");
            UI.DialogueUI.Close();

            TalkTo(SunNpcs[0], SunNpcs[1], SunNpcs[2]);   // 3 of 4 on day 1
            _time.SetDayNumber(2);
            TalkTo(SunNpcs[3]);
            Assert.AreEqual(0, _engine.StepProgress("q_festival_circuit"),
                "the day-1 tally doesn't carry into day 2 — 'within one in-game day'");

            TalkTo(SunNpcs[0], SunNpcs[1], SunNpcs[2]);   // 4 distinct within day 2
            Assert.AreEqual(1, _engine.StepProgress("q_festival_circuit"),
                "a full 4-NPC circuit within day 2 hosts the festival");
            Debug.Log("[P3Test] host_festival tally resets on day rollover, then completes within one day");
        }
    }
}
