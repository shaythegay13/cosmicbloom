// P3-Phase 18 verification — procedural movement-animation stopgap
// (MovementAnimator). Every character sprite is one static Gemini pose (no
// walk-cycle frames — new art batches are blocked on AI Studio credits
// regardless of any code change), so tap-to-move / schedule-walking used to
// just slide a static image across the ground. These tests prove the Visual
// child actually deviates from its rest pose (bob height + squash-stretch
// scale) while PlayerController/NpcController are actively moving the root,
// and settles back to rest once movement stops — for both the player
// (tap-to-move) and an NPC (P3-Phase 16 schedule walking), sharing one
// MovementAnimator component. The screenshot test captures a mid-stride frame
// for human evidence (Logs/phase19_movement_anim.png) and renders a camera,
// so it belongs in the RENDER split, never the -nographics filter.
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
    public class MovementAnimationTests
    {
        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
        }

        [TearDown]
        public void TearDown() => GameEvents.Clear();

        [UnityTest]
        public IEnumerator Player_BobsAndSquashes_WhileMoving_SettlesWhenIdle()
        {
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            RegionWorldBuilder.BuildAll(regions);
            var player = PlayerController.Spawn(new Vector3(20, 20));

            // Teardown lives in `finally` — an assertion failure partway through must not
            // skip cleanup and leak a stale RegionManager singleton into later tests in the
            // same batch (the exact cascade root-caused in Phase17UxFeedbackTests).
            try
            {
                var visual = player.transform.Find("Visual");
                Assert.IsNotNull(visual, "player sprite lives on a child Visual node");
                yield return null;   // MovementAnimator.Init captures the rest pose

                Vector3 basePos = visual.localPosition;
                Vector3 baseScale = visual.localScale;

                player.SetMoveTarget(new Vector3(24, 20, 0f));

                bool sawBob = false, sawSquash = false;
                for (int i = 0; i < 200 && player.MoveTarget != null; i++)
                {
                    player.MoveStep(0.05f);
                    yield return null;   // let MovementAnimator's LateUpdate run against the new position
                    if (Mathf.Abs(visual.localPosition.y - basePos.y) > 0.01f) sawBob = true;
                    if (Mathf.Abs(visual.localScale.x - baseScale.x) > 0.005f) sawSquash = true;
                }
                Assert.IsNull(player.MoveTarget, "walked the full distance within the frame budget");
                Assert.IsTrue(sawBob, "Visual bobbed vertically while the player was walking");
                Assert.IsTrue(sawSquash, "Visual squash-stretched while the player was walking");
                Debug.Log("[P18Test] player Visual bobbed + squash-stretched during tap-to-move ✓");

                for (int i = 0; i < 120; i++) yield return null;   // stopped — should settle back to rest
                Assert.Less((visual.localPosition - basePos).magnitude, 0.02f,
                    "Visual settles back to its rest position once the player stops");
                Assert.Less((visual.localScale - baseScale).magnitude, 0.02f,
                    "Visual settles back to its rest scale once the player stops");
                Debug.Log("[P18Test] player Visual settled back to rest pose after stopping ✓");
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
                Object.DestroyImmediate(regions.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Npc_BobsWhileWalkingToScheduleStop()
        {
            var time = new GameObject("TestTime").AddComponent<TimeManager>();
            time.timeOfDay = 0.30f;   // Morning
            var def = GameDatabase.Npcs.First(n => n.activeAt == "day");
            var center = new Vector3(20f, 20f, 0f);
            var npc = NpcController.Spawn(def, center, 0, 10);

            try
            {
                npc.walkSpeed = 3f;   // slow enough to sample several bobbing frames mid-walk
                var visual = npc.transform.Find("Visual");
                Assert.IsNotNull(visual, "npc sprite lives on a child Visual node");
                yield return null;   // Start() subscribes to OnPeriodChanged; MovementAnimator captures rest pose

                Vector3 basePos = visual.localPosition;

                time.timeOfDay = 0.55f;   // Midday → OnPeriodChanged fires, NPC walks Work → Social

                bool sawBob = false;
                for (int i = 0; i < 400; i++)
                {
                    yield return null;
                    if (Mathf.Abs(visual.localPosition.y - basePos.y) > 0.01f) sawBob = true;
                }
                Assert.IsTrue(sawBob, "NPC Visual bobbed vertically while walking between schedule stops");
                Debug.Log($"[P18Test] npc {def.id} Visual bobbed during schedule walk ✓");
            }
            finally
            {
                Object.DestroyImmediate(npc.gameObject);
                Object.DestroyImmediate(time.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Screenshot_PlayerMidStride()
        {
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            RegionWorldBuilder.BuildAll(regions);
            var player = PlayerController.Spawn(new Vector3(20, 20));
            GameObject camGo = null;
            RenderTexture rt = null;

            try
            {
                yield return null;

                camGo = new GameObject("AnimCam") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 2.5f;
                cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
                cam.clearFlags = CameraClearFlags.SolidColor;

                player.SetMoveTarget(new Vector3(26, 20, 0f));
                for (int i = 0; i < 6; i++) { player.MoveStep(0.05f); yield return null; }   // land mid-stride

                cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);

                const int w = 480, h = 480;
                rt = new RenderTexture(w, h, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                string path = Path.Combine(Application.dataPath, "..", "Logs", "phase19_movement_anim.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[P18Test] mid-stride screenshot -> {path}");
            }
            finally
            {
                if (rt != null) Object.DestroyImmediate(rt);
                if (camGo != null) Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(player.gameObject);
                Object.DestroyImmediate(regions.gameObject);
            }
        }
    }
}
