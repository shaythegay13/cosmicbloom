// Ad hoc verification (2026-07-21): Shay reports dialogue/quest text has never
// been readable — "oriented out of place", clipped at the left edge — seen in a
// Game view set to Full HD landscape at Scale 1.3x. This renders the real
// DialogueUI over the real HUD at two aspects and writes PNGs, so we can tell a
// genuine UI-anchoring bug from a Unity Game-view zoom/crop artifact.
using System.Collections;
using System.IO;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.UI;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class DialogueFramingCheckTests
    {
        [UnityTest]
        public IEnumerator DialogueAndHud_RenderReadable_AtPortraitAndLandscape()
        {
            foreach (var (w, h, label) in new[]
                     {
                         (1080, 1920, "portrait"),
                         (1920, 1080, "landscape"),
                     })
            {
                GameEvents.Clear();
                GameDatabase.LoadAll();
                var regions = new GameObject("FramingRegions").AddComponent<RegionManager>();
                RegionWorldBuilder.BuildAll(regions);
                var state = new GameState(new SaveSnapshot());
                state.Save.coins = 128;
                var engine = new GameObject("FramingEngine").AddComponent<QuestEngine>();
                engine.Bind(state);

                var camGo = new GameObject("FramingCam") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 7f;
                cam.transform.position = new Vector3(20, 20, -10);
                cam.backgroundColor = new Color(0.06f, 0.05f, 0.12f);
                cam.clearFlags = CameraClearFlags.SolidColor;

                var hud = HudUI.Create(state, engine, regions);
                var canvas = UIRoot.Canvas;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;

                // the exact shape of message Shay saw, plus a long one to stress wrapping
                DialogueUI.Show("Celestial Arboretum",
                    "Celestial Arboretum needs 900 coins. Keep farming! " +
                    "This second sentence exists to check that long dialogue wraps inside " +
                    "the panel instead of running off the edge of the screen.", null);

                yield return null;
                yield return null;

                var rt = new RenderTexture(w, h, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                string path = Path.Combine(Application.dataPath, "..", "Logs", $"framing_{label}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[Framing] wrote {path} ({w}x{h})");

                DialogueUI.Close();
                Object.DestroyImmediate(hud.QuestLog.gameObject);
                Object.DestroyImmediate(hud.Map.gameObject);
                Object.DestroyImmediate(hud.Pause.gameObject);
                Object.DestroyImmediate(hud.Ranch.gameObject);
                Object.DestroyImmediate(hud.gameObject);
                Object.DestroyImmediate(engine.gameObject);
                Object.DestroyImmediate(camGo);
                foreach (var def in GameDatabase.Regions)
                {
                    var go = GameObject.Find(RegionWorldBuilder.ToPascal(def.id));
                    if (go != null) Object.DestroyImmediate(go);
                }
                Object.DestroyImmediate(regions.gameObject);
                var ui = GameObject.Find("UIRoot");
                if (ui != null) Object.DestroyImmediate(ui);
                UIRoot.Reset();
                GameEvents.Clear();
            }
        }
    }
}
