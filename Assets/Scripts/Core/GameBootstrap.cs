// Cosmic Bloom — runtime world assembly. The scene stays minimal (managers,
// camera, light, region tilemaps); everything dynamic — player, and in later
// phases NPCs, animals, farm grids, UI — spawns here at Start from game data.
// This avoids per-phase scene surgery (scene YAML edits lost work twice on
// 2026-07-03) and keeps the whole world PlayMode-testable.
using System.Collections.Generic;
using CosmicBloom.World;
using UnityEngine;

namespace CosmicBloom.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        public PlayerController Player { get; private set; }
        public Dictionary<string, Grid> RegionGrids { get; private set; }
        public QuestEngine Quests { get; private set; }
        UI.QuestLogUI _questLog;

        void Start()
        {
            var regions = GetComponent<RegionManager>() != null
                ? GetComponent<RegionManager>()
                : gameObject.AddComponent<RegionManager>();

            RegionGrids = RegionWorldBuilder.BuildAll(regions);   // all 5 regions, data-driven

            var save = GetComponent<Save.SaveManager>();
            if (save != null)
            {
                CropSelection.GrantStarterSeeds(save.State);
                regions.SetCurrentRegion(save.State.Save.currentRegion);   // resume where saved
                regions.OnRegionChanged += id =>
                {
                    save.State.Save.currentRegion = id;
                    save.State.MarkDirty();
                    save.Farming.FastForward();   // catch up crops grown while away
                };

                foreach (var kv in RegionGrids)
                    FarmGrid.Create(kv.Key, kv.Value, save.Farming, save.State);

                StartCoroutine(GrowthTick(save.Farming));
            }

            NpcSpawner.SpawnAll(regions, save != null ? save.State : null);   // all 50, data-placed
            SpawnPlayer(regions);
            AttachCamera();
            DayNightLighting.Create();   // global Light2D driven by TimeManager

            if (save != null)
            {
                LandmarkSpawner.SpawnAll(regions, save.State);
                Quests = gameObject.GetComponent<QuestEngine>() != null
                    ? gameObject.GetComponent<QuestEngine>()
                    : gameObject.AddComponent<QuestEngine>();
                Quests.Bind(save.State);

                AnimalSpawner.SpawnOwned(save.State, regions);

                // floating quest-log toggle until the Phase 9 HUD replaces it
                _questLog = UI.QuestLogUI.Create(Quests);
                var button = UI.UIRoot.MakeButton(UI.UIRoot.Canvas.transform, "QuestsButton", "Quests",
                    () => _questLog.Toggle());
                var rt = (RectTransform)button.transform;
                rt.anchorMin = new Vector2(0.74f, 0.935f);
                rt.anchorMax = new Vector2(0.98f, 0.988f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;

                // floating tool-switch until the Phase 9 HUD replaces it
                UnityEngine.UI.Text toolLabel = null;
                var toolButton = UI.UIRoot.MakeButton(UI.UIRoot.Canvas.transform, "ToolButton", "Tool", () =>
                {
                    var owned = save.State.Save.tools;
                    if (owned.Count == 0) return;
                    int idx = Mathf.Max(0, owned.IndexOf(toolLabel.text));
                    string next = owned[(idx + 1) % owned.Count];
                    Farming.ToolService.SelectTool(save.State, next);
                    toolLabel.text = next;
                });
                toolLabel = toolButton.GetComponentInChildren<UnityEngine.UI.Text>();
                toolLabel.text = save.State.Save.tools.Count > 0 ? save.State.Save.tools[0] : "Tool";
                var trt = (RectTransform)toolButton.transform;
                trt.anchorMin = new Vector2(0.48f, 0.935f);
                trt.anchorMax = new Vector2(0.72f, 0.988f);
                trt.offsetMin = trt.offsetMax = Vector2.zero;
            }
        }

        static System.Collections.IEnumerator GrowthTick(Farming.FarmingSystem farming)
        {
            var wait = new WaitForSeconds(2f);
            while (true)
            {
                yield return wait;
                farming.FastForward();
            }
        }

        void SpawnPlayer(RegionManager regions)
        {
            Vector3 spawn = regions.TryGetBounds(regions.CurrentRegionId, out var b)
                ? b.center
                : Vector3.zero;
            Player = PlayerController.Spawn(spawn);
        }

        void AttachCamera()
        {
            var cam = Camera.main;
            if (cam == null || Player == null) return;
            cam.orthographicSize = 7f;   // frames ~2/3 of a region on a portrait phone
            var follow = cam.GetComponent<CameraFollow>() != null
                ? cam.GetComponent<CameraFollow>()
                : cam.gameObject.AddComponent<CameraFollow>();
            follow.target = Player.transform;
            follow.SnapTo(Player.transform.position);
        }
    }
}
