using System.Linq;
using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class MipChainTests {
        [TestMethod]
        public void LevelsHalveDownToOnePixel() {
            var levels = MipChain.Levels(new uint[8 * 4], 8, 4);
            CollectionAssert.AreEqual(new[] { (4, 2), (2, 1), (1, 1) },
                levels.Select(l => (l.Width, l.Height)).ToArray());
        }

        [TestMethod]
        public void SeeThroughTexelsDontDarkenTheColour() {
            // one solid red texel among three see-through black ones
            var levels = MipChain.Levels(new uint[] { 0xFFFF0000, 0, 0, 0 }, 2, 2);
            Assert.AreEqual(0xFF0000u, levels[0].Pixels[0] & 0xFFFFFF);
        }

        [TestMethod]
        public void ACutOutKeepsItsCoverage() {
            // stripes a texel wide, half solid: averaged alone, every texel of the next level is at half alpha and
            // whether any passes the test is luck; kept, half still pass
            var pixels = new uint[16 * 16];

            for (var i = 0; i < pixels.Length; i++) {
                pixels[i] = (i % 16) % 2 == 0 ? 0xFF00FF00u : 0;
            }

            var level = MipChain.Levels(pixels, 16, 16)[0];
            Assert.AreEqual(0.5f, MipChain.Coverage(pixels), 0.001f);
            Assert.IsTrue(MipChain.Coverage(level.Pixels) >= 0.45f);
        }
    }
}
