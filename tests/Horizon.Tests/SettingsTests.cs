using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Horizon.Tests {
    [TestClass]
    public class SettingsTests {
        private string folder;

        [TestInitialize]
        public void MakeFolder() {
            folder = Path.Combine(Path.GetTempPath(), "horizon-settings-" + Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
        }

        [TestCleanup]
        public void RemoveFolder() {
            Directory.Delete(folder, true);
        }

        [TestMethod]
        public void ToolkitGraphicsAreBroughtOverOnce() {
            var toolkit = Path.Combine(folder, "toolkit.json");
            File.WriteAllText(toolkit, "{\"drawDistance\":{\"farLand\":24},\"grass\":{\"on\":true},"
                + "\"windows\":{\"frame-rate\":{\"visible\":true},\"carried\":{\"visible\":true}},\"theme\":\"Daralet\"}");
            var settings = new Settings(Path.Combine(folder, "settings.json"));

            Assert.IsTrue(settings.ImportFromToolkit(toolkit));
            Assert.AreEqual(24, settings.Data.DrawDistance.FarLand);
            Assert.IsTrue(settings.Data.Windows.ContainsKey("frame-rate"));
            Assert.IsFalse(settings.Data.Windows.ContainsKey("carried"), "only the graphics windows come over");
            Assert.IsFalse(settings.ImportFromToolkit(toolkit), "once only");
        }

        [TestMethod]
        public void NoToolkitMeansDefaults() {
            var settings = new Settings(Path.Combine(folder, "settings.json"));

            Assert.IsFalse(settings.ImportFromToolkit(Path.Combine(folder, "missing.json")));
            Assert.IsTrue(settings.Data.ToolkitImported);
            Assert.IsNotNull(settings.Data.DrawDistance);
        }

        [TestMethod]
        public void SavedSettingsReadBack() {
            var path = Path.Combine(folder, "settings.json");
            var settings = new Settings(path);
            settings.Data.DrawDistance.FarLand = 12;
            settings.Save();

            Assert.AreEqual(12, new Settings(path).Data.DrawDistance.FarLand);
        }
    }
}
