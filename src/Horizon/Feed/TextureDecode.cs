using System;
using System.Collections.Generic;

namespace Horizon.Feed {
    /// <summary>
    /// The scenery's textures as 32-bit colour (0xAARRGGBB), for billboard pictures (Feed/ImpostorRaster.cs): the trees'
    /// are DXT1 (trunks) and DXT5 (foliage cut out by its alpha), and small scenery's 16-bit palette indexes, as a look
    /// at the far scenery round the user showed (November 2026). UtilityBelt's Texture.GetImageColorArray doesn't do DXT.
    /// The DXT formats are the standard S3TC blocks: 4x4 pixels, two RGB565 end colours and 2-bit indexes, DXT5 with
    /// two end alphas and 3-bit alpha indexes before them.
    /// <para>
    /// Decode takes every colour format a dat's texture can be in (SurfacePixelFormat's numbers, as daralet-ACE's
    /// libs/entity has them; the layouts as its dat-loader's Texture reads them), so a season's new trees draw whatever
    /// their textures are in (the user's question, November 2026). Direct3D names formats from the high byte down, so
    /// A8R8G8B8 is stored B, G, R, A.
    /// </para>
    /// </summary>
    public static class TextureDecode {
        public const uint R8G8B8 = 20;
        public const uint A8R8G8B8 = 21;
        public const uint X8R8G8B8 = 22;
        public const uint R5G6B5 = 23;
        public const uint X1R5G5B5 = 24;
        public const uint A1R5G5B5 = 25;
        public const uint A4R4G4B4 = 26;
        public const uint A8 = 28;
        public const uint X4R4G4B4 = 30;
        public const uint A8B8G8R8 = 32;
        public const uint X8B8G8R8 = 33;
        public const uint P8 = 41;
        public const uint L8 = 50;
        public const uint A8L8 = 51;
        public const uint Index16Format = 101;
        public const uint CustomA8B8G8R8 = 241;
        public const uint CustomB8G8R8 = 242;
        public const uint LandscapeR8G8B8 = 243;
        public const uint LandscapeAlpha = 244;
        public const uint RawJpeg = 500;
        public const uint Dxt1Format = 0x31545844;
        public const uint Dxt2Format = 0x32545844;
        public const uint Dxt3Format = 0x33545844;
        public const uint Dxt4Format = 0x34545844;
        public const uint Dxt5Format = 0x35545844;

        /// <summary>A texture's pixels as 0xAARRGGBB, or null for a format not known here.</summary>
        public static uint[] Decode(uint format, byte[] data, int width, int height, IReadOnlyList<uint> palette) {
            switch (format) {
                case Dxt1Format:
                    return Dxt1(data, width, height);
                // DXT2 and DXT4 are DXT3 and DXT5 with the colour multiplied by the alpha already; near enough
                case Dxt2Format:
                case Dxt3Format:
                    return Dxt3(data, width, height);
                case Dxt4Format:
                case Dxt5Format:
                    return Dxt5(data, width, height);
                case Index16Format:
                    return Index16(data, width, height, palette);
                case P8:
                    return Indexed8(data, width, height, palette);
                case RawJpeg:
                    return Jpeg(data);
                case R8G8B8:
                    return Bytes(data, width, height, 3, b => Argb(255, b[2], b[1], b[0]));
                case LandscapeR8G8B8:
                case CustomB8G8R8:
                    return Bytes(data, width, height, 3, b => Argb(255, b[0], b[1], b[2]));
                case A8R8G8B8:
                    return Bytes(data, width, height, 4, b => Argb(b[3], b[2], b[1], b[0]));
                case X8R8G8B8:
                    return Bytes(data, width, height, 4, b => Argb(255, b[2], b[1], b[0]));
                case A8B8G8R8:
                case CustomA8B8G8R8:
                    return Bytes(data, width, height, 4, b => Argb(b[3], b[0], b[1], b[2]));
                case X8B8G8R8:
                    return Bytes(data, width, height, 4, b => Argb(255, b[0], b[1], b[2]));
                case R5G6B5:
                    return Words(data, width, height, w => 0xFF000000 | Rgb565((ushort)w));
                case X1R5G5B5:
                    return Words(data, width, height, w => Argb(255, Five(w >> 10), Five(w >> 5), Five(w)));
                case A1R5G5B5:
                    return Words(data, width, height,
                        w => Argb((w & 0x8000) != 0 ? 255 : 0, Five(w >> 10), Five(w >> 5), Five(w)));
                case A4R4G4B4:
                    return Words(data, width, height,
                        w => Argb(Four(w >> 12), Four(w >> 8), Four(w >> 4), Four(w)));
                case X4R4G4B4:
                    return Words(data, width, height, w => Argb(255, Four(w >> 8), Four(w >> 4), Four(w)));
                case L8:
                    return Bytes(data, width, height, 1, b => Argb(255, b[0], b[0], b[0]));
                case A8L8:
                    return Bytes(data, width, height, 2, b => Argb(b[1], b[0], b[0], b[0]));
                // alpha alone: white, as Direct3D samples it
                case A8:
                case LandscapeAlpha:
                    return Bytes(data, width, height, 1, b => Argb(b[0], 255, 255, 255));
                default:
                    return null;
            }
        }

        /// <summary>DXT3: an explicit 4-bit alpha for each pixel, then a colour block as DXT5's.</summary>
        public static uint[] Dxt3(byte[] data, int width, int height) {
            var pixels = new uint[width * height];
            var alphas = new byte[16];

            for (int by = 0, block = 0; by < height; by += 4) {
                for (var bx = 0; bx < width; bx += 4, block += 16) {
                    if (block + 16 > data.Length) {
                        return pixels;
                    }

                    for (var i = 0; i < 16; i++) {
                        var nibble = (data[block + i / 2] >> (i % 2 * 4)) & 0xF;
                        alphas[i] = (byte)(nibble * 17);
                    }

                    ColourBlock(data, block + 8, pixels, width, height, bx, by, false, alphas);
                }
            }

            return pixels;
        }

        /// <summary>8-bit indexes into a palette of 0xAARRGGBB colours (missing ones transparent).</summary>
        public static uint[] Indexed8(byte[] data, int width, int height, IReadOnlyList<uint> palette) {
            var pixels = new uint[width * height];

            for (var i = 0; i < pixels.Length && i < data.Length; i++) {
                pixels[i] = palette != null && data[i] < palette.Count ? palette[data[i]] : 0;
            }

            return pixels;
        }

        private static uint[] Jpeg(byte[] data) {
            using (var stream = new System.IO.MemoryStream(data))
            using (var bitmap = new System.Drawing.Bitmap(stream)) {
                var pixels = new uint[bitmap.Width * bitmap.Height];

                for (var y = 0; y < bitmap.Height; y++) {
                    for (var x = 0; x < bitmap.Width; x++) {
                        pixels[y * bitmap.Width + x] = (uint)bitmap.GetPixel(x, y).ToArgb();
                    }
                }

                return pixels;
            }
        }

        private static uint[] Bytes(byte[] data, int width, int height, int size, Func<byte[], uint> pixel) {
            var pixels = new uint[width * height];
            var one = new byte[size];

            for (var i = 0; i < pixels.Length && (i + 1) * size <= data.Length; i++) {
                Array.Copy(data, i * size, one, 0, size);
                pixels[i] = pixel(one);
            }

            return pixels;
        }

        private static uint[] Words(byte[] data, int width, int height, Func<int, uint> pixel) {
            var pixels = new uint[width * height];

            for (var i = 0; i < pixels.Length && i * 2 + 1 < data.Length; i++) {
                pixels[i] = pixel(data[i * 2] | (data[i * 2 + 1] << 8));
            }

            return pixels;
        }

        private static uint Argb(int a, int r, int g, int b) {
            return (uint)((a & 255) << 24 | (r & 255) << 16 | (g & 255) << 8 | (b & 255));
        }

        private static int Five(int value) {
            return (value & 31) * 255 / 31;
        }

        private static int Four(int value) {
            return (value & 15) * 17;
        }
        public static uint[] Dxt1(byte[] data, int width, int height) {
            var pixels = new uint[width * height];

            for (int by = 0, block = 0; by < height; by += 4) {
                for (var bx = 0; bx < width; bx += 4, block += 8) {
                    if (block + 8 > data.Length) {
                        return pixels;
                    }

                    ColourBlock(data, block, pixels, width, height, bx, by, true, null);
                }
            }

            return pixels;
        }

        public static uint[] Dxt5(byte[] data, int width, int height) {
            var pixels = new uint[width * height];
            var alphas = new byte[16];

            for (int by = 0, block = 0; by < height; by += 4) {
                for (var bx = 0; bx < width; bx += 4, block += 16) {
                    if (block + 16 > data.Length) {
                        return pixels;
                    }

                    AlphaBlock(data, block, alphas);
                    ColourBlock(data, block + 8, pixels, width, height, bx, by, false, alphas);
                }
            }

            return pixels;
        }

        /// <summary>16-bit indexes into a palette of 0xAARRGGBB colours (missing ones transparent).</summary>
        public static uint[] Index16(byte[] data, int width, int height, IReadOnlyList<uint> palette) {
            var pixels = new uint[width * height];

            for (var i = 0; i < pixels.Length && i * 2 + 1 < data.Length; i++) {
                var index = data[i * 2] | (data[i * 2 + 1] << 8);
                pixels[i] = palette != null && index < palette.Count ? palette[index] : 0;
            }

            return pixels;
        }

        private static void AlphaBlock(byte[] data, int at, byte[] alphas) {
            var a0 = data[at];
            var a1 = data[at + 1];
            var table = new byte[8];
            table[0] = a0;
            table[1] = a1;

            for (var i = 2; i < 8; i++) {
                table[i] = a0 > a1 ? (byte)(((8 - i) * a0 + (i - 1) * a1) / 7)
                    : i < 6 ? (byte)(((6 - i) * a0 + (i - 1) * a1) / 5) : (byte)(i == 6 ? 0 : 255);
            }

            ulong bits = 0;

            for (var i = 0; i < 6; i++) {
                bits |= (ulong)data[at + 2 + i] << (8 * i);
            }

            for (var i = 0; i < 16; i++) {
                alphas[i] = table[(bits >> (3 * i)) & 7];
            }
        }

        private static void ColourBlock(byte[] data, int at, uint[] pixels, int width, int height, int bx, int by,
            bool punchThrough, byte[] alphas) {
            var c0 = (ushort)(data[at] | (data[at + 1] << 8));
            var c1 = (ushort)(data[at + 2] | (data[at + 3] << 8));
            var colours = new uint[4];
            colours[0] = Rgb565(c0);
            colours[1] = Rgb565(c1);

            if (c0 > c1 || !punchThrough) {
                colours[2] = Mix(colours[0], colours[1], 2, 1, 3);
                colours[3] = Mix(colours[0], colours[1], 1, 2, 3);
            }
            else {
                colours[2] = Mix(colours[0], colours[1], 1, 1, 2);
                colours[3] = 0;
            }

            var indexes = (uint)(data[at + 4] | (data[at + 5] << 8) | (data[at + 6] << 16) | (data[at + 7] << 24));

            for (var i = 0; i < 16; i++) {
                var x = bx + (i & 3);
                var y = by + (i >> 2);

                if (x >= width || y >= height) {
                    continue;
                }

                var colour = colours[(indexes >> (2 * i)) & 3];

                if (alphas != null) {
                    colour = (colour & 0x00FFFFFF) | ((uint)alphas[i] << 24);
                }
                else if (colour != 0) {
                    colour |= 0xFF000000;
                }

                pixels[y * width + x] = colour;
            }
        }

        private static uint Rgb565(ushort c) {
            var r = (c >> 11) & 31;
            var g = (c >> 5) & 63;
            var b = c & 31;
            return (uint)(((r * 255 / 31) << 16) | ((g * 255 / 63) << 8) | (b * 255 / 31));
        }

        private static uint Mix(uint a, uint b, int wa, int wb, int total) {
            uint Channel(int shift) => (uint)((((a >> shift) & 255) * wa + ((b >> shift) & 255) * wb) / total) << shift;
            return Channel(16) | Channel(8) | Channel(0);
        }
    }
}
