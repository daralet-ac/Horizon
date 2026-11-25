using System.Collections.Generic;
using System.Linq;
using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class WildnessTests {
        // a town at the map's middle (0, 0): world meters (102 * 240, 102 * 240)
        private const float Middle = 102 * 240;

        private static Wildness OneTown(string type = "Town") {
            return new Wildness(new[] { (type, 0f, 0f), ("Dungeon", 30f, 30f) });
        }

        [TestMethod]
        public void KeptInTownWildFarFromIt() {
            var wildness = OneTown();
            Assert.AreEqual(1, wildness.Places, "only towns and outposts count");
            Assert.AreEqual(0, wildness.At(Middle, Middle), 0.001f);
            Assert.AreEqual(0, wildness.At(Middle + Wildness.TownKept - 1, Middle), 0.001f);
            Assert.AreEqual(1, wildness.At(Middle + Wildness.TownWild + 1, Middle), 0.001f);
        }

        [TestMethod]
        public void ItRisesSmoothlyBetween() {
            var wildness = OneTown();
            var last = -1f;

            for (var d = Wildness.TownKept; d <= Wildness.TownWild; d += 25) {
                var w = wildness.At(Middle, Middle + d);
                Assert.IsTrue(w >= last, $"{d} m: {w} after {last}");
                last = w;
            }

            Assert.AreEqual(0.5f, wildness.At(Middle + (Wildness.TownKept + Wildness.TownWild) / 2, Middle), 0.01f);
        }

        [TestMethod]
        public void OutpostsKeepASmallerPatch() {
            var town = OneTown();
            var outpost = OneTown("Outpost");
            var at = Middle + 200;
            Assert.IsTrue(outpost.At(at, Middle) > town.At(at, Middle));
        }

        [TestMethod]
        public void NoTownsIsWildEverywhere() {
            Assert.AreEqual(1, new Wildness(new (string, float, float)[0]).At(Middle, Middle));
        }

        [TestMethod]
        public void BlocksAreInWorldMeters() {
            // landblock 0x7F7F's south-west corner, plus 10 and 20 meters
            var (x, y) = Wildness.WorldMeters(0x7F7F, 10, 20);
            Assert.AreEqual(0x7F * 192 + 10, x);
            Assert.AreEqual(0x7F * 192 + 20, y);
        }

        [TestMethod]
        public void KeptGrassIsShorterAndSparserThanWild() {
            var terrain = Enumerable.Repeat((ushort)(GrassLayout.LushGrass << 2), 81).ToList();
            var heights = Enumerable.Repeat((byte)10, 81).ToList();
            var table = Enumerable.Range(0, 256).Select(i => i * 2f).ToArray();
            // half density: at full, lush grass already fills every square and wild ground can't grow fuller
            List<GrassClump> Place(float wild) => GrassLayout.Place(0x7F7F0000, terrain, heights, table, 0.5f, null, 96, 96,
                40, (x, y) => wild);

            var kept = Place(0);
            var wildGrass = Place(1);
            Assert.IsTrue(wildGrass.Count > kept.Count * 1.4, $"kept {kept.Count}, wild {wildGrass.Count}");
            Assert.IsTrue(wildGrass.Average(c => c.Scale) > kept.Average(c => c.Scale) * 2,
                $"kept {kept.Average(c => c.Scale)}, wild {wildGrass.Average(c => c.Scale)}");
            Assert.IsTrue(kept.All(c => c.Wildness == 0) && wildGrass.All(c => c.Wildness == 1));

            // kept grass is even: its tallest and shortest close together
            var keptSpread = kept.Max(c => c.Scale) / kept.Min(c => c.Scale);
            var wildSpread = wildGrass.Max(c => c.Scale) / wildGrass.Min(c => c.Scale);
            Assert.IsTrue(wildSpread > keptSpread, $"kept {keptSpread}, wild {wildSpread}");
        }
    }
}
