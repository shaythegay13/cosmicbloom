using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CosmicBloom2DRendererVerify
{
    const string PreviewPath = "Assets/Regions/starlight_vale/_camera_2d_preview.png";

    [MenuItem("Cosmic Bloom/Verify 2D Tilemap Capture")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            CaptureAndLog();
            EditorApplication.ExitPlaymode();
            return;
        }

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.delayCall += () =>
        {
            CaptureAndLog();
            EditorApplication.ExitPlaymode();
        };
    }

    static void CaptureAndLog()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[CosmicBloom] Verify capture failed: no main camera");
            return;
        }

        int w = 512, h = 512;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        RenderPipeline.SubmitRenderRequest(cam, request);

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Color center = tex.GetPixel(w / 2, h / 2);
        Color corner = tex.GetPixel(8, 8);
        Color sky = Hex("2b2140"), t2 = Hex("5fae62");
        int nonSky = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (!Near(tex.GetPixel(x, y), sky, 0.06f))
                    nonSky++;

        File.WriteAllBytes(PreviewPath, tex.EncodeToPNG());
        AssetDatabase.Refresh();
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);

        Debug.Log($"[CosmicBloom] Verify capture nonSky%={nonSky * 100f / (w * h):F1} center={center} tier2={Near(center, t2, 0.12f)} vignetteR={corner.r / Mathf.Max(0.001f, center.r):F3} saved={PreviewPath}");
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    static bool Near(Color a, Color b, float t) =>
        Mathf.Abs(a.r - b.r) <= t && Mathf.Abs(a.g - b.g) <= t && Mathf.Abs(a.b - b.b) <= t;

    static Color Hex(string hex)
    {
        byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
        return new Color32(r, g, b, 255);
    }
}
