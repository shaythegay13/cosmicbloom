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
        public UI.HudUI Hud { get; private set; }

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

            if (save != null)
            {
                var snap = save.State.Save;
                if (snap.hasPlayerPos && Player != null)
                    Player.transform.position = new Vector3(snap.playerX, snap.playerY, 0f);
                if (TimeManager.Instance != null)
                {
                    TimeManager.Instance.timeOfDay = snap.timeOfDay;
                    TimeManager.Instance.SetDayNumber(snap.dayNumber);
                }

                Save.SaveManager.BeforeWrite -= StampSnapshot;
                Save.SaveManager.BeforeWrite += StampSnapshot;
            }
            DayNightLighting.Create();   // global Light2D driven by TimeManager

            if (save != null)
            {
                LandmarkSpawner.SpawnAll(regions, save.State);
                Quests = gameObject.GetComponent<QuestEngine>() != null
                    ? gameObject.GetComponent<QuestEngine>()
                    : gameObject.AddComponent<QuestEngine>();
                Quests.Bind(save.State);

                AnimalSpawner.SpawnOwned(save.State, regions);

                Hud = UI.HudUI.Create(save.State, Quests, regions);
            }
        }

        void StampSnapshot(SaveSnapshot snap)
        {
            if (Player != null)
            {
                snap.playerX = Player.transform.position.x;
                snap.playerY = Player.transform.position.y;
                snap.hasPlayerPos = true;
            }
            if (TimeManager.Instance != null)
            {
                snap.timeOfDay = TimeManager.Instance.timeOfDay;
                snap.dayNumber = TimeManager.Instance.DayNumber;
            }
        }

        void OnDestroy() => Save.SaveManager.BeforeWrite -= StampSnapshot;

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
