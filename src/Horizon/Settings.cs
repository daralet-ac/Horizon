using System;
using System.Collections.Generic;
using System.IO;
using Horizon.Feed;

namespace Horizon {
    /// <summary>
    /// Horizon's settings, in Documents\Decal Plugins\Horizon\settings.json, one set for every character: graphics belong
    /// to the computer, not the character. A change is saved two seconds after the last one, so dragging a window doesn't
    /// write the file every frame.
    /// </summary>
    public class Settings {
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

        /// <summary>The windows of Daralet Toolkit's that Horizon took with the graphics.</summary>
        private static readonly string[] ToolkitWindows = { "frame-rate" };

        private readonly Action<string> log;
        private readonly string filePath;
        private DateTime? changedAt;

        public SettingsData Data { get; private set; }

        /// <param name="filePath">The settings file; the real one when null (tests pass their own).</param>
        /// <param name="log">Where to report a file that can't be read or written.</param>
        public Settings(string filePath = null, Action<string> log = null) {
            this.filePath = filePath ?? DataFolder.For("settings.json");
            this.log = log ?? (_ => { });
            Data = Read(this.filePath) ?? new SettingsData();
        }

        /// <summary>A window's placement by its id, made the first time it's asked for.</summary>
        public WindowPlacement Window(string id, bool visibleByDefault = true) {
            if (!Data.Windows.TryGetValue(id, out var placement) || placement == null) {
                placement = new WindowPlacement { Visible = visibleByDefault };
                Data.Windows[id] = placement;
            }

            return placement;
        }

        /// <summary>Call after changing anything in Data.</summary>
        public void Changed() {
            changedAt = changedAt ?? DateTime.UtcNow;
        }

        /// <summary>Called every frame: saves once the first unsaved change is two seconds old.</summary>
        public void SaveIfDue() {
            if (changedAt.HasValue && DateTime.UtcNow - changedAt.Value > SaveDelay) {
                Save();
            }
        }

        /// <summary>Saves now if anything's changed, for when the plugin shuts down.</summary>
        public void SaveIfChanged() {
            if (changedAt.HasValue) {
                Save();
            }
        }

        public void Save() {
            changedAt = null;

            try {
                Write(filePath, Data);
            }
            catch (Exception ex) {
                log($"Couldn't save the settings to {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Brings the graphics settings over from Daralet Toolkit's settings.json once, the first time Horizon starts with
        /// none of its own: the graphics were the Toolkit's Graphics tab until Horizon was split out of it (November 2026),
        /// and their names in the file are the same. Returns whether anything was read.
        /// </summary>
        public bool ImportFromToolkit(string toolkitFile) {
            if (Data.ToolkitImported) {
                return false;
            }

            Data.ToolkitImported = true;
            Changed();

            if (File.Exists(filePath) && !File.Exists(toolkitFile)) {
                return false;
            }

            var toolkit = Read(toolkitFile);

            if (toolkit == null) {
                return false;
            }

            Data.DrawDistance = toolkit.DrawDistance;
            Data.GraphicsTargetFps = toolkit.GraphicsTargetFps;
            Data.Shading = toolkit.Shading;
            Data.Water = toolkit.Water;
            Data.Image = toolkit.Image;
            Data.Grass = toolkit.Grass;
            Data.SunShadows = toolkit.SunShadows;
            Data.LampLight = toolkit.LampLight;
            Data.ReadCrashRecords = toolkit.ReadCrashRecords;

            foreach (var id in ToolkitWindows) {
                if (toolkit.Windows.TryGetValue(id, out var placement) && placement != null) {
                    Data.Windows[id] = placement;
                }
            }

            return true;
        }

        /// <summary>Writes something as JSON, to a temporary file first, so a crash part way through can't wipe the file.</summary>
        private static void Write<T>(string path, T value) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";

            using (var stream = File.Create(temporary)) {
                Json.Write(stream, value);
            }

            if (File.Exists(path)) {
                File.Replace(temporary, path, null);
            }
            else {
                File.Move(temporary, path);
            }
        }

        /// <summary>The settings in a file, or null when there's no such file or it can't be read (which is logged).</summary>
        private SettingsData Read(string path) {
            try {
                if (!File.Exists(path)) {
                    return null;
                }

                return ReadData(File.ReadAllText(path));
            }
            catch (Exception ex) {
                log($"Couldn't read the settings from {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Settings from JSON, with anything the file left null made again. Throws if it isn't JSON.</summary>
        internal static SettingsData ReadData(string json) {
            var data = Json.Read<SettingsData>(json) ?? new SettingsData();
            var fresh = new SettingsData();

            // a file can say null outright
            data.Windows = data.Windows ?? new Dictionary<string, WindowPlacement>();
            data.DrawDistance = data.DrawDistance ?? fresh.DrawDistance;
            data.Shading = data.Shading ?? fresh.Shading;
            data.Water = data.Water ?? fresh.Water;
            data.Image = data.Image ?? fresh.Image;
            data.Grass = data.Grass ?? fresh.Grass;
            data.SunShadows = data.SunShadows ?? fresh.SunShadows;
            data.LampLight = data.LampLight ?? fresh.LampLight;
            return data;
        }
    }
}
