using System;
using System.Collections.Generic;
using System.Numerics;

namespace Horizon.Feed {
    /// <summary>
    /// A billboard's picture of a model (billboards stage 2; the user's call, November 2026): the model's textured
    /// triangles drawn side on, with no perspective (as a far tree looks), into an image with a see-through background,
    /// a depth buffer keeping the nearer surface. Model space is the game's: x east, y north, z up; the picture looks
    /// along +y, so its x is the model's x and its up the model's z, at PixelsPerMeter. A cut-out surface (the game's
    /// clip maps: foliage) keeps only texels with alpha of half or more, as the game's alpha test does; others are drawn
    /// whole. Both sides of every triangle are drawn: a picture, not the game's renderer. Kept apart from the dats so it
    /// can be tested.
    /// </summary>
    public sealed class ImpostorRaster {
        private readonly List<(Vector3 A, Vector3 B, Vector3 C, Vector2 Ua, Vector2 Ub, Vector2 Uc, int Texture)> triangles =
            new List<(Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, int)>();

        private readonly List<(uint[] Pixels, int Width, int Height, bool CutOut)> textures =
            new List<(uint[], int, int, bool)>();

        /// <summary>A texture to draw with (0xAARRGGBB pixels); its number for AddTriangle.</summary>
        public int AddTexture(uint[] pixels, int width, int height, bool cutOut) {
            textures.Add((pixels, width, height, cutOut));
            return textures.Count - 1;
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, int texture) {
            triangles.Add((a, b, c, ua, ub, uc, texture));
        }

        public int TriangleCount => triangles.Count;

        /// <summary>The model's extent seen side on: left and right (x), bottom and top (z), in meters.</summary>
        public (float Left, float Right, float Bottom, float Top) Bounds() {
            float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;

            foreach (var t in triangles) {
                foreach (var p in new[] { t.A, t.B, t.C }) {
                    left = Math.Min(left, p.X);
                    right = Math.Max(right, p.X);
                    bottom = Math.Min(bottom, p.Z);
                    top = Math.Max(top, p.Z);
                }
            }

            return triangles.Count == 0 ? (0, 0, 0, 0) : (left, right, bottom, top);
        }

        /// <summary>
        /// The picture, height pixels tall and as wide as the model is at that scale (at least 1), as 0xAARRGGBB rows from
        /// the top; with the model's bounds it was drawn to.
        /// </summary>
        public (uint[] Pixels, int Width, int Height, float Left, float Bottom, float MetersPerPixel) Draw(int height) {
            var (left, right, bottom, top) = Bounds();
            var tall = Math.Max(0.01f, top - bottom);
            var scale = height / tall;
            var width = Math.Max(1, (int)Math.Ceiling((right - left) * scale));
            var pixels = new uint[width * height];
            var depth = new float[width * height];

            for (var i = 0; i < depth.Length; i++) {
                depth[i] = float.MaxValue;
            }

            foreach (var t in triangles) {
                Vector3 Screen(Vector3 p) => new Vector3((p.X - left) * scale, (top - p.Z) * scale, p.Y);
                Fill(Screen(t.A), Screen(t.B), Screen(t.C), t.Ua, t.Ub, t.Uc, textures[t.Texture], pixels, depth, width,
                    height);
            }

            return (pixels, width, height, left, bottom, 1 / scale);
        }

        private static void Fill(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc,
            (uint[] Pixels, int Width, int Height, bool CutOut) texture, uint[] pixels, float[] depth, int width,
            int height) {
            var area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);

            if (Math.Abs(area) < 1e-6f) {
                return;
            }

            var minX = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
            var maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
            var minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
            var maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));

            for (var y = minY; y <= maxY; y++) {
                for (var x = minX; x <= maxX; x++) {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
                    var w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
                    var w2 = 1 - w0 - w1;

                    if (w0 < 0 || w1 < 0 || w2 < 0) {
                        continue;
                    }

                    var z = w0 * a.Z + w1 * b.Z + w2 * c.Z;
                    var at = y * width + x;

                    if (z >= depth[at]) {
                        continue;
                    }

                    var u = w0 * ua.X + w1 * ub.X + w2 * uc.X;
                    var v = w0 * ua.Y + w1 * ub.Y + w2 * uc.Y;
                    var texel = Sample(texture, u, v);
                    var alpha = texel >> 24;

                    if (texture.CutOut ? alpha < 128 : alpha == 0) {
                        continue;
                    }

                    depth[at] = z;
                    pixels[at] = texel | 0xFF000000;
                }
            }
        }

        /// <summary>The texel at u, v, wrapping, nearest.</summary>
        private static uint Sample((uint[] Pixels, int Width, int Height, bool CutOut) texture, float u, float v) {
            if (texture.Pixels == null || texture.Width == 0 || texture.Height == 0) {
                return 0xFF808080;
            }

            var x = (int)Math.Floor((u - Math.Floor(u)) * texture.Width) % texture.Width;
            var y = (int)Math.Floor((v - Math.Floor(v)) * texture.Height) % texture.Height;
            return texture.Pixels[y * texture.Width + x];
        }
    }
}
