using System;
using System.IO;

namespace Horizon {
    /// <summary>
    /// Documents\Decal Plugins\Horizon: where everything the plugin writes is kept (settings.json, log.txt, Frame Rate
    /// Logs, Test Runs), for every character on this computer.
    /// </summary>
    public static class DataFolder {
        private static readonly string Plugins =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Decal Plugins");

        public static readonly string Root = Path.Combine(Plugins, "Horizon");

        /// <summary>Daralet Toolkit's settings, which held the graphics settings before Horizon was split out of it.</summary>
        public static readonly string ToolkitSettings = Path.Combine(Plugins, "Daralet Toolkit", "settings.json");

        /// <summary>A file or folder in it.</summary>
        public static string For(string name) {
            return Path.Combine(Root, name);
        }
    }
}
