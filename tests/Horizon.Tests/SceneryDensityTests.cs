using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class SceneryDensityTests {
        /// <summary>How many of a block's worth of places (a tree every 4 meters) are kept.</summary>
        private static int KeptOf(uint landblock, int percent, out int total) {
            var kept = 0;
            total = 0;

            for (var x = 2f; x < 192; x += 4.3f) {
                for (var y = 1f; y < 192; y += 4.7f) {
                    total++;

                    if (SceneryDensity.Keeps(landblock, x, y, percent)) {
                        kept++;
                    }
                }
            }

            return kept;
        }

        [TestMethod]
        public void KeepsAboutThePercentAsked() {
            foreach (var percent in new[] { 25, 50, 75 }) {
                var kept = KeptOf(0xA9B40000, percent, out var total);
                Assert.AreEqual(percent / 100.0, kept / (double)total, 0.05, $"{percent}%");
            }
        }

        [TestMethod]
        public void TheGamesKeepsEverything() {
            Assert.AreEqual(KeptOf(0x12340000, 100, out var total), total);
        }

        [TestMethod]
        public void BlocksAreThinnedDifferently() {
            // the same places in two landblocks: not the same pattern of trees in every block
            var same = 0;

            for (var x = 1f; x < 192; x += 5.1f) {
                if (SceneryDensity.Keeps(0x7D640000, x, 50.5f, 50) == SceneryDensity.Keeps(0x7D650000, x, 50.5f, 50)) {
                    same++;
                }
            }

            Assert.IsTrue(same < 32, $"{same} of 38 alike");
        }

        [TestMethod]
        public void ALowerDensityKeepsOnlyWhatAHigherOneDoes() {
            // so going from 60% to 30% removes trees and never swaps some for others
            for (var x = 1f; x < 192; x += 5.1f) {
                if (SceneryDensity.Keeps(0x33440000, x, 77f, 30)) {
                    Assert.IsTrue(SceneryDensity.Keeps(0x33440000, x, 77f, 60));
                }
            }
        }
    }
}
