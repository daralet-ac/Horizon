using System;
using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class FrameRateLogTests {
        private static FrameSummary Steady(double ms, double seconds) {
            var stats = new FrameStats();
            var at = 0.0;

            while (at < seconds) {
                at += ms / 1000;
                stats.Add(at, ms);
            }

            return stats.Last(at, seconds);
        }

        [TestMethod]
        public void ASecondRowHasEveryColumn() {
            var row = FrameRateLog.Second(new DateTime(2026, 11, 17, 20, 5, 9), Steady(20, 1), 1890,
                "Scenery 10 at 20%, landscape 24");
            var columns = FrameRateLog.Header.Split(',').Length;
            StringAssert.StartsWith(row, "2026-11-17 20:05:09,second,50.0,50.0,20.0,20.00,0,1890,,,");
            StringAssert.EndsWith(row, "\"Scenery 10 at 20%, landscape 24\",,,,,");
            Assert.AreEqual(columns, row.Replace("\"Scenery 10 at 20%, landscape 24\"", "x").Split(',').Length);
        }

        [TestMethod]
        public void AChangeRowHasBeforeAndAfter() {
            var row = FrameRateLog.Change(new DateTime(2026, 11, 17, 20, 5, 9), Steady(10, 5), Steady(25, 5), 1500,
                "Mark 1");
            StringAssert.Contains(row, ",change,40.0,");
            StringAssert.EndsWith(row, ",1500,100.0,100.0,Mark 1,,,,,");
        }

        [TestMethod]
        public void ASecondRowCarriesTheMemoryReading() {
            var memory = new MemorySample {
                UsedMb = 2400, TotalMb = 4096, PrivateMb = 1900, WorkingSetMb = 1200, ManagedMb = 85, LargestFreeMb = 300,
            };
            var row = FrameRateLog.Second(new DateTime(2026, 11, 18, 16, 20, 0), Steady(20, 1), 1696, "off", memory);
            StringAssert.EndsWith(row, ",off,2400,1900,1200,85,300");
            Assert.AreEqual(FrameRateLog.Header.Split(',').Length, row.Split(',').Length);
        }

        [TestMethod]
        public void QuotesOnlyWhenItHasTo() {
            Assert.AreEqual("plain", FrameRateLog.Quote("plain"));
            Assert.AreEqual("\"a, b\"", FrameRateLog.Quote("a, b"));
            Assert.AreEqual("\"say \"\"hi\"\"\"", FrameRateLog.Quote("say \"hi\""));
            Assert.AreEqual("", FrameRateLog.Quote(null));
        }

        [TestMethod]
        public void FileNamesLeaveOutWhatANameCantHold() {
            Assert.AreEqual("Hol_mez 2026-11-17 20-05-09.csv",
                FrameRateLog.FileName("Hol:mez", new DateTime(2026, 11, 17, 20, 5, 9)));
        }

        [TestMethod]
        public void AroundAMarkLeavesOutTheReloadAndTheMarkBefore() {
            var stats = new FrameStats();
            var at = 0.0;

            while (at < 40) {
                // 100 fps until 20 s, then 40 fps
                var ms = at < 20 ? 10 : 25;
                at += ms / 1000.0;
                stats.Add(at, ms);
            }

            var (before, after) = stats.Around(20, 10, 15);
            Assert.AreEqual(100f, before.Fps, 1f);
            Assert.AreEqual(40f, after.Fps, 1f);
            Assert.IsTrue(before.Frames < 800, "only from the mark before's settling on");
        }
    }
}
