using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ImGuiNET;
using UtilityBelt.Service.Views;

namespace Horizon.Ui {
    /// <summary>
    /// The game's UI pictures built into the plugin (Resources\ui, taken from the client by hand): the option slider's
    /// track and thumb, the close button. Each is made a texture the first time it's drawn, once the game's device
    /// exists, and let go at shutdown (Free).
    /// </summary>
    internal static class Art {
        private static readonly Dictionary<string, ManagedTexture> Textures = new Dictionary<string, ManagedTexture>();
        private static bool freed;

        /// <summary>A picture stretched to fill min to max. False when it can't be drawn, for the caller's plain look.</summary>
        public static bool DrawArtBox(ImDrawListPtr drawList, string name, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFF) {
            if (freed) {
                return false;
            }

            if (!Textures.TryGetValue(name, out var texture)) {
                texture = Load(name);
                Textures[name] = texture;
            }

            if (texture == null) {
                return false;
            }

            drawList.AddImage(texture.TexturePtr, min, max, Vector2.Zero, Vector2.One, tint);
            return true;
        }

        /// <summary>Lets go of every texture, at shutdown: a hot-reloaded copy mustn't keep the game's device's memory.</summary>
        public static void Free() {
            freed = true;

            foreach (var texture in Textures.Values) {
                try {
                    texture?.Dispose();
                }
                catch (Exception ex) {
                    PluginCore.LogFile("[art] " + ex.Message);
                }
            }

            Textures.Clear();
        }

        private static ManagedTexture Load(string name) {
            try {
                using (var stream = typeof(Art).Assembly.GetManifestResourceStream($"Horizon.Resources.ui.{name}.png")) {
                    if (stream == null) {
                        return null;
                    }

                    using (var bitmap = new Bitmap(stream)) {
                        return new ManagedTexture(bitmap);
                    }
                }
            }
            catch (Exception ex) {
                PluginCore.LogFile("[art] " + ex);
                return null;
            }
        }
    }
}
