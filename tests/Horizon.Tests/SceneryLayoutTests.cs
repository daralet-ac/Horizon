using System.Collections.Generic;
using System.Linq;
using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class SceneryLayoutTests {
        private const uint Block = 0xA9B40000;

        private static readonly float[] Table = Enumerable.Range(0, 256).Select(i => i * 2f).ToArray();

        /// <summary>A block of one terrain everywhere, no roads, and flat or sloping ground.</summary>
        private static (ushort[] Terrain, byte[] Heights) Land(ushort word, byte height = 10, bool slope = false) {
            var terrain = Enumerable.Repeat(word, 81).ToArray();
            var heights = new byte[81];

            for (var i = 0; i < 81; i++) {
                heights[i] = slope ? (byte)(i / 9 * 5) : height;
            }

            return (terrain, heights);
        }

        private static SceneryObject Tree(float frequency, uint weenie = 0) {
            return new SceneryObject(0x02000123, 12, 12, frequency, 10, 10, 0.8f, 1.2f, 360, weenie);
        }

        private static List<PlacedScenery> Place(ushort[] terrain, byte[] heights, SceneryObject obj, int density = 100) {
            return SceneryLayout.Place(Block, terrain, heights, Table, (t, s) => new uint[] { 0x12000001 },
                id => new[] { obj }, density);
        }

        [TestMethod]
        public void AnObjectThatAlwaysAppearsIsInMostCells() {
            var (terrain, heights) = Land(1 << 2);
            var placed = Place(terrain, heights, Tree(1));

            // 81 vertices, the ones on the far edges often displaced out of the block
            Assert.IsTrue(placed.Count > 40 && placed.Count <= 81, placed.Count.ToString());
            Assert.IsTrue(placed.All(p => p.X >= 0 && p.X <= 192 && p.Y >= 0 && p.Y <= 192));
            Assert.IsTrue(placed.All(p => p.Scale >= 0.8f && p.Scale <= 1.2f));
        }

        [TestMethod]
        public void NeverWhenItsFrequencyIsNoneOrItsAWeenie() {
            var (terrain, heights) = Land(1 << 2);
            Assert.AreEqual(0, Place(terrain, heights, Tree(0)).Count);
            Assert.AreEqual(0, Place(terrain, heights, Tree(1, weenie: 5)).Count);
        }

        [TestMethod]
        public void NothingOnRoads() {
            var (terrain, heights) = Land((1 << 2) | 1);
            Assert.AreEqual(0, Place(terrain, heights, Tree(1)).Count);
        }

        [TestMethod]
        public void TheSameEveryTime() {
            var (terrain, heights) = Land(1 << 2);
            var a = Place(terrain, heights, Tree(0.5f));
            var b = Place(terrain, heights, Tree(0.5f));
            CollectionAssert.AreEqual(a.Select(p => (p.X, p.Y, p.Yaw)).ToList(), b.Select(p => (p.X, p.Y, p.Yaw)).ToList());
        }

        [TestMethod]
        public void DensityTakesSomeAway() {
            var (terrain, heights) = Land(1 << 2);
            var all = Place(terrain, heights, Tree(1)).Count;
            var some = Place(terrain, heights, Tree(1), density: 30).Count;
            Assert.IsTrue(some > 0 && some < all * 0.6, $"{some} of {all}");
        }

        [TestMethod]
        public void NothingInACellWithABuilding() {
            var (terrain, heights) = Land(1 << 2);
            var all = SceneryLayout.Place(Block, terrain, heights, Table, (t, s) => new uint[] { 1 },
                id => new[] { Tree(1) }, 100);
            var cells = new HashSet<int>(all.Select(p => SceneryLayout.CellOf(p.X) * 8 + SceneryLayout.CellOf(p.Y)).Take(5));
            var some = SceneryLayout.Place(Block, terrain, heights, Table, (t, s) => new uint[] { 1 },
                id => new[] { Tree(1) }, 100, cells);
            Assert.IsTrue(some.Count < all.Count);
            Assert.IsFalse(some.Any(p => cells.Contains(SceneryLayout.CellOf(p.X) * 8 + SceneryLayout.CellOf(p.Y))));
        }

        [TestMethod]
        public void OnlyOnTheSlopesItTakes() {
            var (terrain, steep) = Land(1 << 2, slope: true);
            var flatOnly = new SceneryObject(0x02000123, 12, 12, 1, 10, 10, 1, 1, 0, 0, minSlope: 0.95f, maxSlope: 1);
            var slopesOnly = new SceneryObject(0x02000123, 12, 12, 1, 10, 10, 1, 1, 0, 0, minSlope: 0, maxSlope: 0.95f);

            // the slope: 10 m up every 24 m east, an upward part of about 0.92
            Assert.AreEqual(0, Place(terrain, steep, flatOnly).Count);
            Assert.IsTrue(Place(terrain, steep, slopesOnly).Count > 0);
            Assert.AreEqual(0.923f, SceneryLayout.Ground(Block, steep, Table, 30, 30).Upward, 0.001f);
        }

        [TestMethod]
        public void GroundIsTheHeightsAtTheCorners() {
            var (_, heights) = Land(0, slope: true);

            // heights by x: column 0 is 0, column 1 is 5 (so 10 m in the table), and so on
            Assert.AreEqual(0f, SceneryLayout.GroundZ(Block, heights, Table, 0, 0), 0.001f);
            Assert.AreEqual(10f, SceneryLayout.GroundZ(Block, heights, Table, 24, 0), 0.001f);
            Assert.AreEqual(10f, SceneryLayout.GroundZ(Block, heights, Table, 24, 24), 0.001f);
            Assert.AreEqual(5f, SceneryLayout.GroundZ(Block, heights, Table, 12, 7), 0.001f, "half way across a slope");
        }

        [TestMethod]
        public void FlatGroundIsFlatEverywhere() {
            var (_, heights) = Land(0, height: 30);

            for (var x = 0.5f; x < 192; x += 17.3f) {
                for (var y = 0.5f; y < 192; y += 13.1f) {
                    Assert.AreEqual(60f, SceneryLayout.GroundZ(Block, heights, Table, x, y), 0.001f);
                }
            }
        }

        [TestMethod]
        public void LessDetailFlattensAHump() {
            // flat but for the middle vertex (4, 4), 50 high (100 m)
            var heights = new List<byte>(new byte[81]);
            heights[4 * 9 + 4] = 50;
            Assert.AreEqual(100f, SceneryLayout.Ground(Block, heights, Table, 96, 96).Z, 0.001f);
            Assert.AreEqual(100f, SceneryLayout.Ground(Block, heights, Table, 96, 96, 2).Z, 0.001f,
                "every second vertex still has the middle");
            Assert.AreEqual(0f, SceneryLayout.Ground(Block, heights, Table, 96, 96, 8).Z, 0.001f,
                "a block of two triangles has only its corners");
        }

        [TestMethod]
        public void TheGameDrawsFarBlocksInLessDetail() {
            Assert.AreEqual(1, SceneryLayout.DetailStep(5, 5));
            Assert.AreEqual(2, SceneryLayout.DetailStep(6, 5));
            Assert.AreEqual(4, SceneryLayout.DetailStep(8, 5));
            Assert.AreEqual(8, SceneryLayout.DetailStep(9, 5));
        }
    }
}
