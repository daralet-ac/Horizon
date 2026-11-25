using System;
using ImGuiNET;

namespace Horizon.Ui {
    internal partial class FloatingWindow {
        /// <summary>
        /// RenderControls in a row of a form of the caller's, for the Config tab's General sub tab (the Toolkit button):
        /// Enabled, Locked and Reset position, then Size and Background as short sliders with their names in them,
        /// wrapping to the column, with Only when needed for a window that has it.
        /// </summary>
        public void RenderCompactControls(string tip, bool showToggle = true) {
            var first = true;

            if (showToggle) {
                var visible = Visible;

                if (Form.FlowCheck(ref first, $"Enabled##{id}-visible", ref visible, tip)) {
                    Visible = visible;
                }
            }

            if (inUse != null && !alwaysOnlyInUse) {
                var onlyInUse = Placement.OnlyInUse;

                if (Form.FlowCheck(ref first, $"Only when needed##{id}-in-use", ref onlyInUse,
                    "Shows the window only while it has something to show, and hides it the rest of the time, instead of "
                    + "leaving it on screen saying there's nothing.")) {
                    Placement.OnlyInUse = onlyInUse;
                    settings.Changed();
                }
            }

            var locked = Locked;

            if (Form.FlowCheck(ref first, $"Locked##{id}-locked", ref locked, "Unlocked, a window is moved by dragging its "
                + "top edge (the mouse shows the four arrows there) and sized by its other edges. Locked, it stays put. "
                + "Either way, hold Ctrl and drag it from anywhere on it to move it. You can also right-click the window "
                + "to lock or unlock it.")) {
                Locked = locked;
            }

            if (Form.FlowSmallButton(ref first, $"Reset position##{id}-reset",
                "Moves the window back to where it starts out, in case it's off screen.")) {
                ResetPosition();
            }

            var percent = (int)Math.Round(Scale * 100);

            if (Form.FlowSlider(ref first, $"##{id}-scale", ref percent, (int)(WindowPlacement.MinScale * 100),
                (int)(WindowPlacement.MaxScale * 100), "Size %d%%", "How big the window is drawn. Ctrl+click to type a "
                + "size. You can also right-click the window for Bigger, Smaller and Normal size.")) {
                Scale = percent / 100f;
            }

            var background = (int)Math.Round(Look.BackgroundAlpha * 100);

            if (Form.FlowSlider(ref first, $"##{id}-background", ref background, 0, 100, "Background %d%%",
                "How solid the window's background is. At 0% there's no box at all, only what's in it.")) {
                Look.Background = background / 100f;
                settings.Changed();
            }
        }
    }
}
