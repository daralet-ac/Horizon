using System.Collections.Generic;
using System.Linq;
using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class WaterTests {
        /// <summary>A terrain word of a type, as the cell dat packs it (road bits low, the type from bit 2).</summary>
        private static ushort Terrain(int type) {
            return (ushort)(type << 2);
        }

        private static List<ushort> Block(int type) {
            return Enumerable.Repeat(Terrain(type), 81).ToList();
        }

        [TestMethod]
        public void OnlyTheFiveWaterTypesAreWater() {
            for (var type = 0; type < 0x20; type++) {
                Assert.AreEqual(type >= 0x10 && type <= 0x14, WaterMap.IsWater(Terrain(type)), $"type {type:X}");
            }

            Assert.IsTrue(WaterMap.IsWater((ushort)(Terrain(0x11) | 0x3)), "road bits don't matter");
        }

        [TestMethod]
        public void TheMapCoversTheBlocksRoundHome() {
            Assert.AreEqual(73, WaterMap.Side(4));
            Assert.AreEqual(9, WaterMap.Side(0));
        }

        [TestMethod]
        public void HomesWaterLandsWhereTheShaderLooks() {
            // only home (0x8080) is water, all of it; radius 1, so home's corner is vertex (8, 8) of a 25 a side
            var map = WaterMap.Build(0x8080, 1, block => block == 0x8080 ? Block(0x11) : Block(0x1));
            var side = WaterMap.Side(1);
            Assert.AreEqual(25, side);
            Assert.AreEqual(255, map[8 * side + 8], "home's south-west corner");
            Assert.AreEqual(255, map[16 * side + 16], "home's north-east corner");
            Assert.AreEqual(0, map[7 * side + 8], "just south of home is land");
            Assert.AreEqual(81, map.Count(v => v == 255));
        }

        [TestMethod]
        public void NorthIsAlongTheRowsAndEastAlongEachRow() {
            // a block whose east edge (x = 8) only is water
            var terrain = Block(0x1);

            for (var y = 0; y <= 8; y++) {
                terrain[8 * 9 + y] = Terrain(0x10);
            }

            var map = WaterMap.Build(0x4040, 0, block => terrain);
            Assert.AreEqual(255, map[0 * 9 + 8], "the south row's east end");
            Assert.AreEqual(255, map[8 * 9 + 8], "the north row's east end");
            Assert.AreEqual(0, map[4 * 9 + 0], "the west edge");
        }

        [TestMethod]
        public void BlocksOffTheWorldOrMissingAreLand() {
            var map = WaterMap.Build(0x0000, 1, block => block == 0x0000 ? Block(0x14) : null);
            Assert.AreEqual(81, map.Count(v => v == 255), "only home: the rest is off the world or unknown");
        }

        [TestMethod]
        public void WaterSettingsHaveTheirDefaultsAndLimits() {
            var water = Json.Read<WaterOptions>("{}");
            Assert.IsFalse(water.On);
            Assert.AreEqual(35, water.ClampedStrength);
            Assert.IsTrue(water.Ripples && water.Fresnel && water.Glints && water.Blur && water.SoftShore);

            water.Strength = 130;
            water.RippleSize = 2;
            Assert.AreEqual(100, water.ClampedStrength);
            Assert.AreEqual(10, water.ClampedRippleSize);

            water.On = true;
            water.Glints = false;
            StringAssert.Contains(water.Summary(), "fresnel");
            Assert.IsFalse(water.Summary().Contains("glints"));
        }

        [TestMethod]
        public void TheWaterTestStepsRead() {
            var steps = TestScript.Parse(new[] { "set water 1", "set ripplesize 150", "set softshore 0" });
            Assert.IsTrue(steps.TrueForAll(s => s.Kind == "set"));
            Assert.AreEqual(150, steps[1].Number);
        }
    }
}
