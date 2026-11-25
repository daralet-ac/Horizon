using System;
using System.Runtime.InteropServices;

namespace Horizon.Ui {
    /// <summary>
    /// Fog (DrawDistanceOptions.Fog; the user's call, November 2026): the game's distance fog thinned, so the far
    /// landscape and billboards show through it. The game draws it with Direct3D's linear fog, from a start distance
    /// (none) to an end (all fog colour), which it works out each frame from the sky for the time of day, eased between
    /// changes, in its landscape update, and hands to RenderDeviceD3D::SetFFFogProperties_ (0x5A41B0, thiscall: the colour,
    /// start and end; it sets D3DRS_FOGCOLOR, FOGSTART and FOGEND, only when they change) from one call, at 0x5066BB. That
    /// call is pointed here (AcClient.Hook, as on_road): start and end are stretched by 100 over the percent, so 50% puts
    /// the fog twice as far out, and the game's colour and its own on/off (Disable Distance Fog) are left alone. Seen in
    /// the game at midday: 150 to 2400 meters. Hooked only while below 100%; taken out at shutdown, after which the
    /// game's next call sets its own distances again. Untested in the game when written.
    /// </summary>
    internal sealed partial class DrawDistance {
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void SetFogCall(IntPtr device, IntPtr colour, float start, float end);

        private static AcClient.Hook fogHook;

        // both kept while the hook is in, so they're never collected while the client can call them
        private static SetFogCall fogCall;
        private static SetFogCall gameSetFog;

        /// <summary>The fog's distances are multiplied by this, read on the game's thread every frame.</summary>
        private static volatile float fogStretch = 1;

        private static float lastFogStart;
        private static float lastFogEnd;

        /// <summary>The fog at this percent of the game's (100 for the game's own).</summary>
        private static void ApplyFog(int percent) {
            if (percent >= 100) {
                UnhookFog();
                return;
            }

            if (fogHook == null && !HookFog()) {
                return;
            }

            var stretch = 100f / Math.Max(1, percent);

            if (Math.Abs(stretch - fogStretch) > 0.001f) {
                PluginCore.LogFile($"[draw-distance] fog {percent}%: the game's {lastFogStart:0}-{lastFogEnd:0} m "
                    + $"drawn as {lastFogStart * stretch:0}-{lastFogEnd * stretch:0} m");
                fogStretch = stretch;
            }
        }

        private static bool HookFog() {
            var entry = (int)AcClient.Entrypoint.RenderDeviceD3D__SetFFFogProperties_;
            gameSetFog = Marshal.GetDelegateForFunctionPointer<SetFogCall>((IntPtr)entry);
            fogCall = SetFog;
            var hooked = new AcClient.Hook(entry, (int)AcClient.Call.RenderDeviceD3D__SetFFFogProperties_0x005066BB);

            if (!hooked.Setup(fogCall)) {
                PluginCore.LogFile("[draw-distance] couldn't hook the fog at 0x005066BB");
                fogCall = null;
                return false;
            }

            fogHook = hooked;
            return true;
        }

        /// <summary>The game's call put back. A hook left in after the plugin's gone would crash the game.</summary>
        private static void UnhookFog() {
            fogStretch = 1;

            if (fogHook == null) {
                return;
            }

            try {
                fogHook.Remove();
                PluginCore.LogFile("[draw-distance] fog back to the game's");
            }
            catch (Exception ex) {
                PluginCore.LogFile("[draw-distance] " + ex);
            }

            fogHook = null;
            fogCall = null;
        }

        private static void SetFog(IntPtr device, IntPtr colour, float start, float end) {
            var stretch = 1f;

            try {
                lastFogStart = start;
                lastFogEnd = end;
                stretch = fogStretch;
            }
            catch {
                // nothing may escape into the game's frame; its own fog stands
            }

            gameSetFog(device, colour, start * stretch, end * stretch);
        }
    }
}
