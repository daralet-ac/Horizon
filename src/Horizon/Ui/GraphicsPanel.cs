using System;
using System.Numerics;
using Decal.Adapter;

namespace Horizon.Ui {
    /// <summary>
    /// The Horizon window: every graphics setting, the budget and presets pinned at the top (GraphicsPanel.DrawDistance's
    /// RenderGraphics). It also makes and owns each graphics piece, which run whether or not the window shows.
    /// Was Daralet Toolkit's Config tab's Graphics sub tab until Horizon was split out of it (November 2026).
    /// </summary>
    /// <remarks>
    /// Split across files: <c>.DrawDistance</c> (the page and the view distance rows), <c>.Budget</c>, <c>.Draft</c>
    /// (changes wait for Apply), <c>.Shading</c>, <c>.Water</c>, <c>.Grass</c>, <c>.Image</c>.
    /// </remarks>
    internal sealed partial class GraphicsPanel : IDisposable {
        private readonly Settings settings;
        private readonly FloatingWindow window;
        private bool disposed;

        public GraphicsPanel(Settings settings) {
            this.settings = settings;
            drawDistance = new DrawDistance(settings);
            frameRate = new FrameRateWindow(settings);
            // before the shading, so its occlusion darkens the grass too: Decal calls RenderFrame in subscription order
            grass = new Grass(settings);
            farLand = new FarLand(settings, drawDistance);
            depthShading = new DepthShading(settings);
            testRunner = new TestRunner(settings, drawDistance, Commit);
            window = new FloatingWindow("horizon", "Horizon", settings, new Vector2(120, 120), Render,
                visibleByDefault: false, defaultSize: new Vector2(520, 640), showInBar: true);
        }

        public FloatingWindow Window => window;

        /// <summary>Shows the window, or hides it if it shows (/horizon, the key).</summary>
        public void Toggle() {
            var placement = settings.Window("horizon", visibleByDefault: false);
            placement.Visible = !placement.Visible;
            settings.Changed();
        }

        private void Render() {
            if (disposed) {
                return;
            }

            if (CoreManager.Current.CharacterFilter.LoginStatus != 3) {
                Widgets.TextDisabled("Not in the world yet.");
                return;
            }

            RenderGraphics();
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            Step(() => window.Dispose());
            // the game's own draw distance back first: its code patched while it's in use
            Step(() => drawDistance.Dispose());
            Step(() => grass.Dispose());
            Step(() => farLand.Dispose());
            Step(() => depthShading.Dispose());
            Step(() => testRunner.Dispose());
            Step(() => frameRate.Dispose());
        }

        /// <summary>Each piece let go on its own, so one that fails doesn't leave the next's hooks in the game.</summary>
        private static void Step(Action step) {
            try {
                step();
            }
            catch (Exception ex) {
                PluginCore.LogFile("[horizon] " + ex);
            }
        }
    }
}
