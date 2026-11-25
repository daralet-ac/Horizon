using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class FrameStatsTests {
        /// <summary>So many seconds of frames this long each, ending at the time given.</summary>
        private static double Run(FrameStats stats, double from, double seconds, double ms) {
            var at = from;

            while (at < from + seconds) {
                at += ms / 1000;
                stats.Add(at, ms);
            }

            return at;
        }

        [TestMethod]
        public void SteadyFramesGiveTheirRate() {
            var stats = new FrameStats();
            var now = Run(stats, 0, 10, 20);
            var last = stats.Last(now, 5);
            Assert.AreEqual(50f, last.Fps, 0.5f);
            Assert.AreEqual(20f, last.AverageMs, 0.01f);
            Assert.AreEqual(50f, last.LowFps, 0.5f);
        }

        [TestMethod]
        public void OneLowFindsTheStutters() {
            var stats = new FrameStats();
            var now = Run(stats, 0, 10, 10);

            // two slow frames in 1000: the average barely moves, the 1% low doesn't either; twenty of them, and it does
            for (var i = 0; i < 20; i++) {
                now += 0.1;
                stats.Add(now, 100);
            }

            var last = stats.Last(now, 30);
            Assert.AreEqual(10f, last.LowFps, 0.01f);
            Assert.AreEqual(100f, last.WorstMs, 0.01f);
        }

        [TestMethod]
        public void PausesAreLeftOutAndCounted() {
            var stats = new FrameStats();
            var now = Run(stats, 0, 5, 20);
            now += 5;
            stats.Add(now, 5000);
            now = Run(stats, now, 5, 20);
            var last = stats.Last(now, 30);
            Assert.AreEqual(50f, last.Fps, 0.5f);
            Assert.AreEqual(1, last.Hitches);
        }

        [TestMethod]
        public void BeforeAndAfterAMarkLeaveOutTheReload() {
            var stats = new FrameStats();
            var now = Run(stats, 0, 20, 10);
            stats.Mark(now, "Scenery 5");
            now = Run(stats, now, FrameStats.SettleSeconds, 200);
            now = Run(stats, now, 10, 25);
            var around = stats.AroundLastMark(now, 10).Value;
            Assert.AreEqual("Scenery 5", around.Label);
            Assert.AreEqual(100f, around.Before.Fps, 1f);
            Assert.AreEqual(40f, around.After.Fps, 1f);
        }

        [TestMethod]
        public void NoMarkNothingToCompare() {
            var stats = new FrameStats();
            Run(stats, 0, 5, 10);
            Assert.IsNull(stats.AroundLastMark(5, 10));
        }

        [TestMethod]
        public void OldFramesAndMarksGo() {
            var stats = new FrameStats();
            stats.Mark(0.5, "old");
            var now = Run(stats, 0, FrameStats.KeepSeconds + 10, 50);
            Assert.AreEqual(0, stats.Marks.Count);
            Assert.AreEqual(0, stats.Between(0, 5).Frames);
        }

        [TestMethod]
        public void BucketsShowEachStretchsRateAndGaps() {
            var stats = new FrameStats();
            var now = Run(stats, 0, 2, 10);
            now = Run(stats, now + 2, 2, 20);
            var buckets = stats.Buckets(now, 6, 3);
            Assert.AreEqual(100f, buckets[0], 1f);
            Assert.AreEqual(0f, buckets[1]);
            Assert.AreEqual(50f, buckets[2], 1f);
        }
    }
}
