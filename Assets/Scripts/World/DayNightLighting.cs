// Cosmic Bloom — visible day/night: a URP 2D global light driven by
// TimeManager.AmbientIntensity(), tinted by the current region's
// ambientTints from regions.json, plus the camera background following the
// region's skyColor darkened at night.
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicBloom.World
{
    public class DayNightLighting : MonoBehaviour
    {
        Light2D _globalLight;
        Camera _cam;

        public static DayNightLighting Create()
        {
            var go = new GameObject("DayNightLighting");
            var lighting = go.AddComponent<DayNightLighting>();

            var lightGo = new GameObject("GlobalLight2D");
            lightGo.transform.SetParent(go.transform, false);
            lighting._globalLight = lightGo.AddComponent<Light2D>();
            lighting._globalLight.lightType = Light2D.LightType.Global;
            lighting._cam = Camera.main;
            if (lighting._cam != null) Assign2DRenderer(lighting._cam);
            return lighting;
        }

        /// <summary>Point a camera at the pipeline's Renderer2DData so Light2D applies
        /// (the pipeline default renderer index is the non-2D one).</summary>
        public static void Assign2DRenderer(Camera cam)
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            var list = pipeline.rendererDataList;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] is Renderer2DData)
                {
                    cam.GetUniversalAdditionalCameraData().SetRenderer(i);
                    return;
                }
            }
            Debug.LogWarning("[DayNight] no Renderer2DData in pipeline — Light2D will not render");
        }

        void Update()
        {
            var time = TimeManager.Instance;
            if (time == null || _globalLight == null) return;

            _globalLight.intensity = time.AmbientIntensity();
            var def = RegionManager.Instance != null
                ? GameDatabase.Region(RegionManager.Instance.CurrentRegionId)
                : null;
            _globalLight.color = TintFor(def, time);

            if (_cam != null && def != null)
            {
                var sky = ProceduralSprites.ParseHex(def.skyColor);
                _cam.backgroundColor = sky * Mathf.Lerp(0.35f, 1f, time.AmbientIntensity());
            }
        }

        /// <summary>Blend the region's ambientTints (dusk/night/deep-night) by time of day;
        /// full white at noon so daytime ground colors read true.</summary>
        static Color TintFor(RegionDef def, TimeManager time)
        {
            if (def == null || def.ambientTints == null || def.ambientTints.Count < 3)
                return Color.white;

            float t = time.timeOfDay;
            if (time.IsDay)
            {
                // sunrise/sunset shoulders ease through tint[0]
                float noonness = 1f - Mathf.Abs(t - 0.5f) / 0.28f;
                return Color.Lerp(ProceduralSprites.ParseHex(def.ambientTints[0]), Color.white,
                                  Mathf.Clamp01(noonness) * 0.85f + 0.15f);
            }

            // night: blend tint[1] → tint[2] toward midnight
            float midnightness = t >= 0.5f ? Mathf.InverseLerp(0.78f, 1f, t) : Mathf.InverseLerp(0.25f, 0f, t);
            return Color.Lerp(ProceduralSprites.ParseHex(def.ambientTints[1]),
                              ProceduralSprites.ParseHex(def.ambientTints[2]),
                              Mathf.Clamp01(midnightness));
        }
    }
}
