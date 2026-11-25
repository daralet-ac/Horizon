using System;
using Horizon.Feed;
using ImGuiNET;

namespace Horizon.Ui {
    /// <summary>
    /// The version you have, in the Horizon section, and an Update button over the budget while a newer release is out
    /// (UpdateCheck, as Daralet Toolkit's top line has them). It only tells: the player runs the new installer.
    /// </summary>
    internal sealed partial class GraphicsPanel {
        private UpdateCheck updates;

        /// <summary>The update check, made by PluginCore; null in tests or if it couldn't be made.</summary>
        public UpdateCheck Updates {
            set => updates = value;
        }

        /// <summary>A row with the Update button, before the budget, only while a newer release is out.</summary>
        private void RenderUpdateRow() {
            var newer = updates?.Newer;

            if (newer == null) {
                return;
            }

            var version = newer.Version.ToString(3);
            ImGui.PushStyleColor(ImGuiCol.Text, Colors.Warning);
            var clicked = ImGui.SmallButton($"Update: {version}");
            ImGui.PopStyleColor();
            Widgets.Tip($"Horizon {version} is out. Opens its download page: close the game and Decal, then run its "
                + "installer. Your settings are kept.");

            if (clicked) {
                try {
                    // the default browser, as any link
                    System.Diagnostics.Process.Start(newer.Url);
                }
                catch (Exception ex) {
                    PluginCore.Log($"Couldn't open {newer.Url}: {ex.Message}");
                }
            }
        }

        /// <summary>The Version row of the Horizon section, so a player can say which they have.</summary>
        private void RenderVersionRow() {
            if (updates == null) {
                return;
            }

            Form.Label("Version", "The version of Horizon you have.");
            Widgets.TextDisabled($"v{updates.Current.ToString(3)}");
            Widgets.Tip(updates.Status);
        }
    }
}
