using Horizon.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class DrawDistanceRampTests {
        [TestMethod]
        public void GoesUpAStepAtATime() {
            var steps = new System.Collections.Generic.List<int>();
            var at = 1;

            while (at != 16) {
                at = DrawDistanceRamp.Next(at, 16, DrawDistanceRamp.SceneryStep);
                steps.Add(at);
            }

            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }, steps);
        }

        [TestMethod]
        public void StepsOnlyOnceTheLastHasSettled() {
            Assert.IsFalse(DrawDistanceRamp.MayStep(1, 5), "too soon after the last step");
            Assert.IsFalse(DrawDistanceRamp.MayStep(5, 0.5), "still loading: a slow frame half a second ago");
            Assert.IsTrue(DrawDistanceRamp.MayStep(1.5, 1));
        }

        [TestMethod]
        public void RaisesTheLandscapeBeforeTheScenery() {
            Assert.AreEqual((1, 18), DrawDistanceRamp.OneAtATime(1, 15, 2, 18));
            Assert.AreEqual((2, 18), DrawDistanceRamp.OneAtATime(1, 18, 2, 18));
            Assert.AreEqual((1, 15), DrawDistanceRamp.OneAtATime(3, 18, 1, 15), "going down, both at once");
        }

        [TestMethod]
        public void GoesDownAllAtOnce() {
            Assert.AreEqual(1, DrawDistanceRamp.Next(16, 1, DrawDistanceRamp.SceneryStep));
        }

        [TestMethod]
        public void SceneryStaysTwoInsideTheLandscape() {
            // landscape 10 with scenery 14 crashed the game: the edge blocks in full detail
            Assert.AreEqual(8, DrawDistanceRamp.SceneryFor(14, 10));
            Assert.AreEqual(14, DrawDistanceRamp.SceneryFor(14, 24));
            Assert.AreEqual(1, DrawDistanceRamp.SceneryFor(5, 3));
            Assert.AreEqual(1, DrawDistanceRamp.SceneryFor(1, 1));
        }

        [TestMethod]
        public void StaysWhenThere() {
            Assert.AreEqual(24, DrawDistanceRamp.Next(24, 24, DrawDistanceRamp.LandscapeStep));
        }
    }
}
