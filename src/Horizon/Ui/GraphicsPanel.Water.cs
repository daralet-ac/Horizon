using ImGuiNET;

namespace Horizon.Ui {
    /// <summary>
    /// The Water rows (Ui/DepthShading.Water.cs): reflections on or off, how strong, and each piece of the look its own
    /// switch, so they can be mixed and matched (the user's call, November 2026).
    /// </summary>
    internal sealed partial class GraphicsPanel {
        private int? draggingWaterStrength;
        private int? draggingRippleSize;

        private void RenderWater() {
            var options = WaterSettings;
            Form.Label("Reflections", "Reflections of the hills, trees and sky on water. Worked out from the game's "
                + "depth.");

            if (ImGui.Checkbox("Reflect on water##water-on", ref options.On)) {
                Changed();
            }

            ImGui.BeginDisabled(!options.On);
            Form.Label("Strength", "How much the reflection covers the game's water.");
            var strength = draggingWaterStrength ?? options.ClampedStrength;

            if (DragSlider("##water-strength", ref strength, 0, 100, "%d%%", ref draggingWaterStrength)) {
                options.Strength = strength;
                Changed();
            }

            Form.Label("Ripples", "Small moving waves that break the reflection up.");

            if (ImGui.Checkbox("##water-ripples", ref options.Ripples)) {
                Changed();
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(!options.Ripples);
            var size = draggingRippleSize ?? options.ClampedRippleSize;

            if (DragSlider("##water-ripple-size", ref size, 10, 300, "%d%% size", ref draggingRippleSize)) {
                options.RippleSize = size;
                Changed();
            }

            ImGui.EndDisabled();
            Form.Label("Look", "Each part of the water's look, to mix and match.");
            var first = true;
            var changed = Form.FlowCheck(ref first, "Fresnel##water-fresnel", ref options.Fresnel,
                "Little reflection looking straight down, more at a glancing angle, as real water.");
            changed |= Form.FlowCheck(ref first, "Sun glints##water-glints", ref options.Glints,
                "The sun's glitter on the ripples.");
            changed |= Form.FlowCheck(ref first, "Distance blur##water-blur", ref options.Blur,
                "Reflections softer the further what they show is from the water.");
            changed |= Form.FlowCheck(ref first, "Soft shores##water-shore", ref options.SoftShore,
                "The reflection fades out toward the shore instead of ending in a hard line.");

            if (changed) {
                Changed();
            }

            ImGui.EndDisabled();
        }
    }
}
