using System.Linq;
using System.Numerics;
using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class ImpostorTests {
        [TestMethod]
        public void Dxt1DecodesItsEndColours() {
            // one 4x4 block: colour 0 pure red (RGB565 0xF800), colour 1 pure blue (0x001F), every pixel index 0 then 1
            var block = new byte[] { 0x00, 0xF8, 0x1F, 0x00, 0x00, 0x00, 0x55, 0x55 };
            var pixels = TextureDecode.Dxt1(block, 4, 4);
            Assert.AreEqual(0xFFFF0000u, pixels[0], "the first two rows use colour 0");
            Assert.AreEqual(0xFF0000FFu, pixels[15], "the last two rows use colour 1");
        }

        [TestMethod]
        public void Dxt1PunchThroughIsSeeThrough() {
            // colour 0 not above colour 1: index 3 is transparent
            var block = new byte[] { 0x1F, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF };
            Assert.IsTrue(TextureDecode.Dxt1(block, 4, 4).All(p => p == 0));
        }

        [TestMethod]
        public void Dxt5TakesItsAlpha() {
            // alpha 0 = 255, alpha 1 = 0, every alpha index 1 (all transparent); colour white
            var block = new byte[] { 255, 0, 0x49, 0x92, 0x24, 0x49, 0x92, 0x24, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0 };
            var pixels = TextureDecode.Dxt5(block, 4, 4);
            Assert.IsTrue(pixels.All(p => p >> 24 == 0), "alpha index 1 is the second end alpha, 0");
        }

        [TestMethod]
        public void Index16LooksUpThePalette() {
            var pixels = TextureDecode.Index16(new byte[] { 1, 0, 0, 0 }, 2, 1, new uint[] { 0xFF111111, 0xFF222222 });
            CollectionAssert.AreEqual(new[] { 0xFF222222u, 0xFF111111u }, pixels);
        }

        [TestMethod]
        public void ASquareFillsThePicture() {
            var raster = new ImpostorRaster();
            var green = raster.AddTexture(new[] { 0xFF00FF00u }, 1, 1, false);
            var a = new Vector3(0, 0, 0);
            var b = new Vector3(2, 0, 0);
            var c = new Vector3(2, 0, 2);
            var d = new Vector3(0, 0, 2);
            raster.AddTriangle(a, b, c, Vector2.Zero, Vector2.Zero, Vector2.Zero, green);
            raster.AddTriangle(a, c, d, Vector2.Zero, Vector2.Zero, Vector2.Zero, green);
            var (pixels, width, height, _, _, metersPerPixel) = raster.Draw(16);
            Assert.AreEqual(16, width);
            Assert.AreEqual(2f / 16, metersPerPixel, 1e-6f);
            Assert.IsTrue(pixels.All(p => p == 0xFF00FF00u));
        }

        [TestMethod]
        public void CutOutLeavesLowAlphaSeeThrough() {
            var raster = new ImpostorRaster();
            var faint = raster.AddTexture(new[] { 0x40FFFFFFu }, 1, 1, true);
            raster.AddTriangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), Vector2.Zero, Vector2.Zero,
                Vector2.Zero, faint);
            Assert.IsTrue(raster.Draw(8).Pixels.All(p => p == 0));
        }

        [TestMethod]
        public void TheNearerSurfaceWins() {
            var raster = new ImpostorRaster();
            var red = raster.AddTexture(new[] { 0xFFFF0000u }, 1, 1, false);
            var blue = raster.AddTexture(new[] { 0xFF0000FFu }, 1, 1, false);

            // looking along +y: y 1 is behind y 0
            foreach (var (y, texture) in new[] { (1f, red), (0f, blue) }) {
                raster.AddTriangle(new Vector3(0, y, 0), new Vector3(4, y, 0), new Vector3(0, y, 4), Vector2.Zero,
                    Vector2.Zero, Vector2.Zero, texture);
            }

            // the bottom left corner of an 8 by 8 picture, inside both triangles
            Assert.AreEqual(0xFF0000FFu, raster.Draw(8).Pixels[7 * 8]);
        }
    }
}
