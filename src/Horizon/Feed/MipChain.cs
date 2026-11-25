using System;
using System.Collections.Generic;

namespace Horizon.Feed {
    /// <summary>
    /// Smaller copies of a cut-out picture (a billboard's) for drawing it far off (mipmaps). Without them the GPU picks
    /// one texel of the full picture per pixel, which changes from frame to frame as the camera moves: far billboards
    /// sparkled. Each level halves the last (2 by 2 averaged, colours weighted by alpha so the see-through texels'
    /// black doesn't darken the edges). Averaging alpha alone thins a cut-out as it shrinks (a texel half covered falls
    /// under the alpha test's half), so far trees would go see-through; each level's alpha is scaled so as many texels
    /// pass the test, in proportion, as in the full picture (alpha-to-coverage preserving, as in Castaño's "Computing
    /// Alpha Mipmaps"). Pixels are 0xAARRGGBB.
    /// </summary>
    public static class MipChain {
        public const int AlphaTest = 128;

        /// <summary>Every level after the first, down to 1 by 1, with each one's width and height.</summary>
        public static List<(uint[] Pixels, int Width, int Height)> Levels(uint[] pixels, int width, int height) {
            var levels = new List<(uint[], int, int)>();
            var coverage = Coverage(pixels);
            var last = pixels;
            int w = width, h = height;

            while (w > 1 || h > 1) {
                var nw = Math.Max(1, w / 2);
                var nh = Math.Max(1, h / 2);
                var next = Halve(last, w, h, nw, nh);
                KeepCoverage(next, coverage);
                levels.Add((next, nw, nh));
                last = next;
                w = nw;
                h = nh;
            }

            return levels;
        }

        /// <summary>The share of texels that pass the alpha test.</summary>
        public static float Coverage(uint[] pixels) {
            var passing = 0;

            foreach (var p in pixels) {
                if (p >> 24 >= AlphaTest) {
                    passing++;
                }
            }

            return pixels.Length == 0 ? 0 : (float)passing / pixels.Length;
        }

        private static uint[] Halve(uint[] pixels, int w, int h, int nw, int nh) {
            var result = new uint[nw * nh];

            for (var y = 0; y < nh; y++) {
                for (var x = 0; x < nw; x++) {
                    long a = 0, r = 0, g = 0, b = 0;
                    var count = 0;

                    for (var dy = 0; dy < 2; dy++) {
                        for (var dx = 0; dx < 2; dx++) {
                            var sx = Math.Min(w - 1, x * 2 + dx);
                            var sy = Math.Min(h - 1, y * 2 + dy);
                            var p = pixels[sy * w + sx];
                            var pa = p >> 24;
                            a += pa;
                            r += ((p >> 16) & 0xFF) * pa;
                            g += ((p >> 8) & 0xFF) * pa;
                            b += (p & 0xFF) * pa;
                            count++;
                        }
                    }

                    var alpha = a / count;
                    result[y * nw + x] = a == 0 ? 0
                        : (uint)alpha << 24 | (uint)(r / a) << 16 | (uint)(g / a) << 8 | (uint)(b / a);
                }
            }

            return result;
        }

        /// <summary>Alpha scaled (a binary search on the scale) until the passing share is the full picture's.</summary>
        private static void KeepCoverage(uint[] pixels, float wanted) {
            float low = 0, high = 4, scale = 1;

            for (var i = 0; i < 10; i++) {
                scale = (low + high) / 2;
                var passing = 0;

                foreach (var p in pixels) {
                    if ((p >> 24) * scale >= AlphaTest) {
                        passing++;
                    }
                }

                if ((float)passing / pixels.Length < wanted) {
                    low = scale;
                }
                else {
                    high = scale;
                }
            }

            for (var i = 0; i < pixels.Length; i++) {
                var alpha = Math.Min(255, (int)Math.Round((pixels[i] >> 24) * scale));
                pixels[i] = (uint)alpha << 24 | (pixels[i] & 0xFFFFFF);
            }
        }
    }
}
