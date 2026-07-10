// Cosmic Bloom — placeholder-art helpers. All characters/props use simple
// procedural sprites tinted from data palettes until final art lands.
using System.Collections.Generic;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class ProceduralSprites
    {
        static readonly Dictionary<string, Sprite> _cache = new();

        static Material _litMaterial;

        /// <summary>Shared 2D-lit sprite material so the Light2D day/night cycle
        /// affects world sprites (the old FarmPlotSprite material is unlit).</summary>
        public static Material LitMaterial
        {
            get
            {
                if (_litMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                    if (shader != null) _litMaterial = new Material(shader);
                }
                return _litMaterial;
            }
        }

        /// <summary>Rounded capsule (body + head silhouette), white — tint via SpriteRenderer.color.</summary>
        public static Sprite Capsule(int w = 26, int h = 34)
        {
            string key = $"capsule_{w}x{h}";
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0, 0, 0, 0);
            float cx = w / 2f;
            float headR = w * 0.32f;
            float headCy = h * 0.75f;
            float bodyHalfW = w * 0.30f;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool head = (x - cx) * (x - cx) + (y - headCy) * (y - headCy) <= headR * headR;
                    bool body = Mathf.Abs(x - cx) <= bodyHalfW && y >= h * 0.12f && y <= h * 0.68f;
                    tex.SetPixel(x, y, head || body ? Color.white : clear);
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.15f), 32f);
            _cache[key] = s;
            return s;
        }

        /// <summary>Solid square with transparent border inset, white — tint via renderer.</summary>
        public static Sprite Square(int size = 32, int inset = 6)
        {
            string key = $"square_{size}_{inset}";
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, x >= inset && x < size - inset && y >= inset && y < size - inset
                        ? Color.white : clear);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            _cache[key] = s;
            return s;
        }

        /// <summary>Small four-legged blob silhouette for animals, white — tint via renderer.</summary>
        public static Sprite Critter(int w = 30, int h = 22)
        {
            string key = $"critter_{w}x{h}";
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0, 0, 0, 0);
            float bx = w * 0.45f, by = h * 0.55f, brx = w * 0.34f, bry = h * 0.30f;   // body ellipse
            float hx = w * 0.82f, hy = h * 0.62f, hr = h * 0.24f;                     // head circle

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dbx = (x - bx) / brx, dby = (y - by) / bry;
                    bool body = dbx * dbx + dby * dby <= 1f;
                    bool head = (x - hx) * (x - hx) + (y - hy) * (y - hy) <= hr * hr;
                    bool legs = y < h * 0.30f && ((x > w * 0.18f && x < w * 0.28f) || (x > w * 0.58f && x < w * 0.68f));
                    tex.SetPixel(x, y, body || head || legs ? Color.white : clear);
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), 32f);
            _cache[key] = s;
            return s;
        }

        /// <summary>Small rounded stone silhouette for ambient ground decoration, white — tint via renderer.</summary>
        public static Sprite Rock(int w = 20, int h = 13)
        {
            string key = $"rock_{w}x{h}";
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0, 0, 0, 0);
            float cx = w * 0.5f, cy = h * 0.45f, rx = w * 0.46f, ry = h * 0.42f;
            float bumpX = w * 0.68f, bumpY = h * 0.6f, bumpR = h * 0.28f;   // extra lump for an irregular silhouette

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    bool body = dx * dx + dy * dy <= 1f;
                    bool bump = (x - bumpX) * (x - bumpX) + (y - bumpY) * (y - bumpY) <= bumpR * bumpR;
                    tex.SetPixel(x, y, body || bump ? Color.white : clear);
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.15f), 32f);
            _cache[key] = s;
            return s;
        }

        /// <summary>Small 3-blade grass tuft for ambient ground decoration, white — tint via renderer.</summary>
        public static Sprite Tuft(int w = 14, int h = 18)
        {
            string key = $"tuft_{w}x{h}";
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0, 0, 0, 0);
            float baseY = h * 0.1f;
            (float tipX, float tipY, float width)[] blades =
            {
                (w * 0.22f, h * 0.95f, w * 0.16f),
                (w * 0.5f,  h * 1.0f,  w * 0.14f),
                (w * 0.78f, h * 0.9f,  w * 0.16f),
            };

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool onBlade = false;
                    foreach (var (tipX, tipY, width) in blades)
                    {
                        if (y < baseY || y > tipY) continue;
                        float t = (y - baseY) / Mathf.Max(1f, tipY - baseY);   // 0 at base, 1 at tip
                        float halfW = Mathf.Lerp(width, 0f, t) * 0.5f;
                        float centerX = Mathf.Lerp(w * 0.5f, tipX, t);
                        if (Mathf.Abs(x - centerX) <= halfW) { onBlade = true; break; }
                    }
                    tex.SetPixel(x, y, onBlade ? Color.white : clear);
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.05f), 32f);
            _cache[key] = s;
            return s;
        }

        /// <summary>"#rrggbb" → Color; magenta on bad input (visually obvious placeholder failure).</summary>
        public static Color ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            return ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c)
                ? c : Color.magenta;
        }
    }
}
