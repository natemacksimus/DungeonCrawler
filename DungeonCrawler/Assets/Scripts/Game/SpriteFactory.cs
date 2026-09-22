using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawler.Game
{
    public enum ShapeKind
    {
        Square,
        Disc,
        Ring,
        Diamond,
        Triangle,
        Flask,
        Scroll,
        Blade,
        Shield,
        Coin,
        Stairs
    }

    /// <summary>
    /// Builds every sprite in the game at runtime, so the project needs no imported art. Shapes are
    /// drawn as white alpha masks and tinted per entity by the renderer.
    /// </summary>
    public static class SpriteFactory
    {
        const int ShapeResolution = 32;
        static readonly Dictionary<ShapeKind, Sprite> Cache = new Dictionary<ShapeKind, Sprite>();
        static Sprite _white;
        static Material _unlit;

        /// <summary>
        /// Shared unlit material for every world sprite. Going unlit keeps the map readable no matter
        /// what 2D lights the scene happens to contain.
        /// </summary>
        public static Material UnlitMaterial
        {
            get
            {
                if (_unlit == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    _unlit = new Material(shader) { name = "dc_sprite_unlit", hideFlags = HideFlags.DontSave };
                }
                return _unlit;
            }
        }

        /// <summary>A plain 1x1 white sprite, used for UI images and solid fills.</summary>
        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var texture = NewTexture(1, 1, "dc_white");
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    _white = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    _white.name = "dc_white";
                }
                return _white;
            }
        }

        public static Sprite Shape(ShapeKind kind)
        {
            Sprite cached;
            if (Cache.TryGetValue(kind, out cached) && cached != null) return cached;

            int n = ShapeResolution;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    pixels[y * n + x] = Paint(kind, x, y, n);

            var tex = NewTexture(n, n, "dc_shape_" + kind);
            tex.SetPixels32(pixels);
            tex.Apply();

            // One tile is one world unit, so pixelsPerUnit equals the shape resolution.
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.name = "dc_shape_" + kind;
            Cache[kind] = sprite;
            return sprite;
        }

        static Color32 Paint(ShapeKind kind, int x, int y, int n)
        {
            // Work in -1..1 space with a small margin so shapes do not touch tile edges.
            float u = (x + 0.5f) / n * 2f - 1f;
            float v = (y + 0.5f) / n * 2f - 1f;
            bool on = false;

            switch (kind)
            {
                case ShapeKind.Square:
                    on = Mathf.Abs(u) < 0.72f && Mathf.Abs(v) < 0.72f;
                    break;
                case ShapeKind.Disc:
                    on = u * u + v * v < 0.62f;
                    break;
                case ShapeKind.Ring:
                {
                    float r2 = u * u + v * v;
                    on = r2 < 0.66f && r2 > 0.26f;
                    break;
                }
                case ShapeKind.Diamond:
                    on = Mathf.Abs(u) + Mathf.Abs(v) < 0.88f;
                    break;
                case ShapeKind.Triangle:
                    on = v < 0.7f && v > -0.62f && Mathf.Abs(u) < (0.7f - v) * 0.62f;
                    break;
                case ShapeKind.Flask:
                {
                    bool neck = Mathf.Abs(u) < 0.2f && v > 0.34f && v < 0.74f;
                    bool body = u * u + (v + 0.16f) * (v + 0.16f) < 0.34f;
                    on = neck || body;
                    break;
                }
                case ShapeKind.Scroll:
                {
                    bool sheet = Mathf.Abs(u) < 0.5f && Mathf.Abs(v) < 0.66f;
                    bool rods = Mathf.Abs(u) < 0.68f && (Mathf.Abs(v - 0.58f) < 0.1f || Mathf.Abs(v + 0.58f) < 0.1f);
                    on = sheet || rods;
                    break;
                }
                case ShapeKind.Blade:
                {
                    bool edge = Mathf.Abs(u - v) < 0.14f && u > -0.5f && v > -0.5f;
                    bool guard = Mathf.Abs(u + v + 0.7f) < 0.12f && Mathf.Abs(u - v) < 0.42f;
                    on = edge || guard;
                    break;
                }
                case ShapeKind.Shield:
                {
                    bool top = Mathf.Abs(u) < 0.56f && v > -0.1f && v < 0.68f;
                    bool taper = v <= -0.1f && v > -0.76f && Mathf.Abs(u) < 0.56f * (1f + (v + 0.1f) / 0.66f);
                    on = top || taper;
                    break;
                }
                case ShapeKind.Coin:
                    on = u * u + v * v < 0.34f;
                    break;
                case ShapeKind.Stairs:
                {
                    // Three descending steps.
                    for (int step = 0; step < 3 && !on; step++)
                    {
                        float left = -0.78f + step * 0.26f;
                        float top = 0.7f - step * 0.42f;
                        on = u > left && u < 0.78f && v < top && v > top - 0.22f;
                    }
                    break;
                }
            }

            return on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }

        /// <summary>
        /// Turns a full-map pixel buffer into one sprite. <paramref name="pixelsPerTile"/> becomes the
        /// pixels-per-unit so the sprite lands exactly on the tile grid.
        /// </summary>
        public static Sprite FromPixels(Color32[] pixels, int width, int height, int pixelsPerTile, string name)
        {
            var tex = NewTexture(width, height, name);
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0f, 0f), pixelsPerTile);
            sprite.name = name;
            return sprite;
        }

        public static Texture2D NewTexture(int width, int height, string name)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            return tex;
        }
    }
}
