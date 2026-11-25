using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    /// <summary>Every colour format a dat's texture can be in, one pixel each, as daralet-ACE's dat-loader reads them.</summary>
    [TestClass]
    public class TextureDecodeTests {
        private static uint One(uint format, params byte[] data) {
            return TextureDecode.Decode(format, data, 1, 1, new uint[] { 0xFF102030, 0x80405060 })[0];
        }

        [TestMethod]
        public void ThirtyTwoBitFormatsReadTheirByteOrder() {
            // stored low byte first: A8R8G8B8 is B, G, R, A
            Assert.AreEqual(0x80112233u, One(TextureDecode.A8R8G8B8, 0x33, 0x22, 0x11, 0x80));
            Assert.AreEqual(0xFF112233u, One(TextureDecode.X8R8G8B8, 0x33, 0x22, 0x11, 0x00), "X is opaque");
            Assert.AreEqual(0x80112233u, One(TextureDecode.A8B8G8R8, 0x11, 0x22, 0x33, 0x80));
            Assert.AreEqual(0xFF112233u, One(TextureDecode.X8B8G8R8, 0x11, 0x22, 0x33, 0x00));
            Assert.AreEqual(0x80112233u, One(TextureDecode.CustomA8B8G8R8, 0x11, 0x22, 0x33, 0x80));
        }

        [TestMethod]
        public void TwentyFourBitFormats() {
            Assert.AreEqual(0xFF112233u, One(TextureDecode.R8G8B8, 0x33, 0x22, 0x11));
            Assert.AreEqual(0xFF112233u, One(TextureDecode.LandscapeR8G8B8, 0x11, 0x22, 0x33),
                "the landscape's own is red first, as the dat-loader reads it");
            Assert.AreEqual(0xFF112233u, One(TextureDecode.CustomB8G8R8, 0x11, 0x22, 0x33));
        }

        [TestMethod]
        public void SixteenBitFormats() {
            Assert.AreEqual(0xFFFF0000u, One(TextureDecode.R5G6B5, 0x00, 0xF8), "pure red");
            Assert.AreEqual(0xFF00FF00u, One(TextureDecode.X1R5G5B5, 0xE0, 0x03), "pure green");
            Assert.AreEqual(0x000000FFu, One(TextureDecode.A1R5G5B5, 0x1F, 0x00), "blue, the alpha bit clear");
            Assert.AreEqual(0xFF0000FFu, One(TextureDecode.A1R5G5B5, 0x1F, 0x80));
            Assert.AreEqual(0x88FF0000u, One(TextureDecode.A4R4G4B4, 0x00, 0x8F));
            Assert.AreEqual(0xFFFF0000u, One(TextureDecode.X4R4G4B4, 0x00, 0x0F));
        }

        [TestMethod]
        public void GreyAlphaAndPaletteFormats() {
            Assert.AreEqual(0xFF404040u, One(TextureDecode.L8, 0x40));
            Assert.AreEqual(0x80404040u, One(TextureDecode.A8L8, 0x40, 0x80));
            Assert.AreEqual(0x40FFFFFFu, One(TextureDecode.A8, 0x40), "alpha alone samples as white");
            Assert.AreEqual(0x80405060u, One(TextureDecode.P8, 1));
            Assert.AreEqual(0u, One(TextureDecode.P8, 7), "past the palette, see-through");
            Assert.AreEqual(0x80405060u, One(TextureDecode.Index16Format, 1, 0));
        }

        [TestMethod]
        public void Dxt3TakesItsExplicitAlpha() {
            // alpha nibbles: every pixel 0x8 (136); colour white (both ends 0xFFFF), every index 0
            var block = new byte[] { 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0 };
            var pixels = TextureDecode.Decode(TextureDecode.Dxt3Format, block, 4, 4, null);
            Assert.IsTrue(pixels.All(p => p == 0x88FFFFFFu), pixels[0].ToString("X8"));
        }

        [TestMethod]
        public void TheDxtFormatsAreFoundByTheirFourCc() {
            var dxt1 = new byte[] { 0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0 };
            Assert.AreEqual(0xFFFF0000u, TextureDecode.Decode(0x31545844, dxt1, 4, 4, null)[0], "DXT1");
            Assert.IsNotNull(TextureDecode.Decode(TextureDecode.Dxt5Format, new byte[16], 4, 4, null));
        }

        [TestMethod]
        public void JpegIsDecoded() {
            byte[] jpeg;

            using (var bitmap = new Bitmap(8, 8)) {
                using (var g = Graphics.FromImage(bitmap)) {
                    g.Clear(Color.FromArgb(255, 200, 40, 40));
                }

                using (var stream = new MemoryStream()) {
                    bitmap.Save(stream, ImageFormat.Jpeg);
                    jpeg = stream.ToArray();
                }
            }

            var pixel = TextureDecode.Decode(TextureDecode.RawJpeg, jpeg, 8, 8, null)[0];
            Assert.AreEqual(0xFFu, pixel >> 24);
            Assert.IsTrue(((pixel >> 16) & 255) > 180 && ((pixel >> 8) & 255) < 70, pixel.ToString("X8"));
        }

        [TestMethod]
        public void UnknownFormatsSayTheyAreUnknown() {
            Assert.IsNull(TextureDecode.Decode(64, new byte[16], 2, 2, null), "V16U16, a bump map");
        }

        [TestMethod]
        public void ShortDataLeavesTheRestSeeThrough() {
            var pixels = TextureDecode.Decode(TextureDecode.A8R8G8B8, new byte[] { 1, 2, 3, 255 }, 2, 1, null);
            Assert.AreEqual(0xFF030201u, pixels[0]);
            Assert.AreEqual(0u, pixels[1]);
        }
    }
}
