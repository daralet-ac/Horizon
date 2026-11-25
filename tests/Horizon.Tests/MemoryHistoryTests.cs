using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class MemoryHistoryTests {
        private static MemoryHistory Growing(double perMinute, double minutes) {
            var history = new MemoryHistory();

            for (var at = 0.0; at <= minutes * 60; at += 2) {
                history.Add(new MemorySample { At = at, TotalMb = 4096, UsedMb = 2000 + (long)(perMinute * at / 60) });
            }

            return history;
        }

        [TestMethod]
        public void GrowthIsTheSlopeInMegabytesAMinute() {
            var history = Growing(3, 20);
            Assert.AreEqual(3, history.GrowthPerMinute(0).Value, 0.1);
            Assert.AreEqual(3, history.GrowthPerMinute(5 * 60).Value, 0.1);
        }

        [TestMethod]
        public void ASpikeDoesNotDecideIt() {
            var history = Growing(0, 10);
            history.Add(new MemorySample { At = 10 * 60 + 2, TotalMb = 4096, UsedMb = 2300 });
            Assert.IsTrue(history.GrowthPerMinute(0).Value < 2, "one load late on barely moves the line");
        }

        [TestMethod]
        public void UnderAMinuteSaysNothing() {
            Assert.IsNull(Growing(5, 0.5).GrowthPerMinute(0));
            Assert.IsNull(new MemoryHistory().GrowthPerMinute(0));
        }

        [TestMethod]
        public void TwoHoursAreKept() {
            var history = Growing(0, 150);
            Assert.IsTrue(history.Span <= MemoryHistory.KeepSeconds);
        }

        [TestMethod]
        public void MinutesUntilTheGuard() {
            var now = new MemorySample { TotalMb = 4096, UsedMb = 3000 };
            Assert.AreEqual(584 / 2.0, MemoryHistory.MinutesUntil(now, 2, 512).Value, 0.01);
            Assert.IsNull(MemoryHistory.MinutesUntil(now, 0, 512));
        }
    }
}
