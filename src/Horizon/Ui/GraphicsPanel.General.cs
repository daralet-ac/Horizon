using ImGuiNET;

namespace Horizon.Ui {
    /// <summary>
    /// The page's last section, Horizon itself: its windows' text size, and opting in to reading the game's crash records.
    /// </summary>
    internal sealed partial class GraphicsPanel {
        private void RenderGeneral() {
            Form.Section("Horizon");

            if (!Form.BeginForm("graphics-general")) {
                return;
            }

            Form.Label("Text size", "The size of the text in Horizon's windows.");
            var scale = settings.Data.ClampedTextScale * 100;
            ImGui.SetNextItemWidth(Form.ControlWidth);

            if (Sliders.SliderFloat("##text-size", ref scale, SettingsData.MinTextScale * 100, SettingsData.MaxTextScale * 100,
                "%.0f%%")) {
                settings.Data.TextScale = scale / 100;
                settings.Changed();
            }

            Form.Label("Crash records", "For reporting a crash: written to log.txt, in Horizon's folder in Documents.");

            if (ImGui.Checkbox("Read the game's crash records##read-crash-records", ref settings.Data.ReadCrashRecords)) {
                settings.Changed();
            }

            Widgets.Tip("At start, reads Windows' records of the game crashing or hanging (acclient.exe only) and "
                + "writes where it crashed to log.txt. Nothing is sent anywhere.");
            ImGui.EndTable();
        }
    }
}
