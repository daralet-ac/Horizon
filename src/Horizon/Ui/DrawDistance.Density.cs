using System;
using System.Runtime.InteropServices;
using Horizon.Feed;

namespace Horizon.Ui {
    /// <summary>
    /// Scenery density (DrawDistanceOptions.SceneryDensity; the user's call, November 2026): fewer trees, bushes and
    /// rocks, for frame rate. Scenery 4 to 5 landblocks took the game from about 160 fps to 14 on a mountain top, and
    /// plainer models didn't help: the cost is the number of objects, every part of each worked through every frame.
    /// <para>
    /// CLandBlock::get_land_scenes (0x5311A0) makes a block's scenery from its terrain's scene types: for each object a
    /// scene might hold it works out a place, checks it's inside the block, then asks CLandBlock::on_road (0x530D30,
    /// thiscall, the place's Vector3 in the block's meters) at 0x53150E, and skips the object if that says it's on a
    /// road; only then is the object made (CPhysicsObj::makeObject, 0x514470) and added (add_static_object). That call
    /// is pointed here (AcClient.Hook, as get_degrade): the game's answer first, and an object not on a road is said to
    /// be one when SceneryDensity.Keeps says so, by the landblock's id (the CLandBlock's +0x28, which the game puts in
    /// the new object's Position at 0x53155D) and the place, so the same trees go each time a block loads. Only scenery
    /// goes through it: buildings and the dats' placed objects (CLandBlockInfo's) are made elsewhere and stay.
    /// </para>
    /// Every block's density is decided as it loads, so a change loads the landscape again. It thins everywhere alike,
    /// the landblock you're in too: a block keeps its scenery while you walk into it (the game only rebuilds a block whose
    /// detail changes), so thinning by distance from you would leave the near ones thin. Untested in the game when written.
    /// </summary>
    internal sealed partial class DrawDistance {
        private const int LandBlockId = 0x28;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int OnRoadCall(IntPtr landblock, IntPtr place);

        private static AcClient.Hook densityHook;

        // both kept while the hook is in, so they're never collected while the client can call them
        private static OnRoadCall densityCall;
        private static OnRoadCall gameOnRoad;

        /// <summary>The percent of scenery kept, read on the game's thread as blocks load.</summary>
        private static volatile int densityPercent = 100;

        /// <summary>The density the loaded landscape was made with.</summary>
        private int appliedDensity = 100;

        /// <summary>Sets the density; true when the landscape has to be loaded again for it to show.</summary>
        private bool ApplyDensity(int percent) {
            if (percent == appliedDensity) {
                return false;
            }

            if (percent < 100 && densityHook == null && !HookOnRoad()) {
                return false;
            }

            densityPercent = percent;
            PluginCore.LogFile($"[draw-distance] scenery density {appliedDensity}% -> {percent}%");
            appliedDensity = percent;

            return true;
        }

        private static bool HookOnRoad() {
            var entry = (int)AcClient.Entrypoint.CLandBlock__on_road;
            gameOnRoad = Marshal.GetDelegateForFunctionPointer<OnRoadCall>((IntPtr)entry);
            densityCall = OnRoad;
            var hooked = new AcClient.Hook(entry, (int)AcClient.Call.CLandBlock__on_road_0x0053150E);

            if (!hooked.Setup(densityCall)) {
                PluginCore.LogFile("[draw-distance] couldn't hook the scenery's road check at 0x0053150E");
                densityCall = null;
                return false;
            }

            densityHook = hooked;
            return true;
        }

        /// <summary>The game's call put back. A hook left in after the plugin's gone would crash the game.</summary>
        private static void UnhookOnRoad() {
            densityPercent = 100;

            if (densityHook == null) {
                return;
            }

            try {
                densityHook.Remove();
            }
            catch (Exception ex) {
                PluginCore.LogFile("[draw-distance] " + ex);
            }

            densityHook = null;
            densityCall = null;
        }

        private static unsafe int OnRoad(IntPtr landblock, IntPtr place) {
            var onRoad = gameOnRoad(landblock, place);

            try {
                var percent = densityPercent;

                if (onRoad != 0 || landblock == IntPtr.Zero || place == IntPtr.Zero) {
                    return onRoad;
                }

                // past Scenery, in Ground detail's rings: no trees from the game (DrawDistance.Ground.cs)
                if (!TreesAllowed(*(uint*)((byte*)landblock + LandBlockId))) {
                    return 1;
                }

                if (percent >= 100) {
                    return onRoad;
                }

                var at = (float*)place;
                var id = *(uint*)((byte*)landblock + LandBlockId);
                return SceneryDensity.Keeps(id, at[0], at[1], percent) ? 0 : 1;
            }
            catch {
                // nothing may escape into the game's loading; the game's own answer stands
                return onRoad;
            }
        }
    }
}
