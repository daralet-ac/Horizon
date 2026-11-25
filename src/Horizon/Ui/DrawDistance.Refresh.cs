using System;
using System.Runtime.InteropServices;
using Horizon.Feed;

namespace Horizon.Ui {
    /// <summary>
    /// The scenery's rings changed in place, without loading the whole landscape again (November 2026).
    /// <para>
    /// SmartBox::set_mid_radius lets go of every block and loads them all again, and for a moment the old and new
    /// overlap: in the game each step up took 600 to 800 MB more than it kept, and the step from scenery 11 to 13
    /// crashed in CPolygon::make_plane (0x539153) with 1.6 GB "free", a 32-bit game's free space being in pieces. Yet as
    /// you walk, the game changes blocks' detail without that: LScape::update_loadpoint (0x507730), on entering another
    /// landblock, calls the grid update at 0x506E00 (thiscall: the load point's cell id, the move in blocks east and
    /// north, and 1 to start again from nothing; ret 10h), which with no move and 0 fills any empty place, then asks
    /// get_block_orient (0x507509) of every block and remakes only those whose detail changed (notify_change_size,
    /// 0x507525, for one that was full detail; CLandBlockStruct's generate, 0x5338F0, then its cells); then 0x5066D0
    /// (thiscall: the LScape's +0x14 and whether the load point's block changed; ret 8), which works out the cells in
    /// sight. A block that's become full detail gets its trees from LScape::grab_visible_cells (0x505920:
    /// init_buildings at 0x505947, init_static_objs at 0x505989), as when you walk. So after the rings are patched, this
    /// calls the two as update_loadpoint does, for the load point it already has (the LScape's +0x10), with no move.
    /// </para>
    /// <para>
    /// But nothing calls grab_visible_cells after those two: the game does it when the player moves into another cell
    /// (every 24 meters), CLandCell::release_cells (0x533AE0) for the old one then CLandCell::grab_visible_cells (0x533AD0)
    /// for the new, each a jump to the LScape's (at [0x8456F0]) release_visible_cells (0x5059B0) and
    /// grab_visible_cells (0x505920), no arguments. So the first build stepped up with the far blocks' ground at full
    /// detail and no trees on them until you walked: in the game, a density change stepped back up to 14 and the
    /// distance stayed bare. The second called that release and grab itself, and the trees came, but the game crashed
    /// on the next step (an AccessViolationException in RefreshBlocks). Now it makes each new full-detail block's
    /// objects with the three per-block calls grab_visible_cells makes (MakeBlocksObjects), and leaves the cells in
    /// sight to the game.
    /// </para>
    /// The landscape's radius still needs set_mid_radius (its block grid is made again at the new size), and so does
    /// Scenery density (every block's scenery is made again). `[draw-distance]` lines in log.txt. Untested in the game
    /// when written.
    /// </summary>
    internal sealed partial class DrawDistance {
        private const uint GridUpdateAddress = 0x506E00;
        private const uint SightUpdateAddress = 0x5066D0;
        private const int LScapeLoadPoint = 0x10;
        private const int LScapeSight = 0x14;
        private const int LScapeWidth = 0x04;
        private const int LScapeBlocks = 0x08;
        private const int BlockSideCells = 0x48;
        private const int BlockStaticCount = 0x104;
        private const uint InitBuildingsAddress = 0x530AC0;
        private const uint InitStaticObjsAddress = 0x531780;
        private const uint InitCellsAddress = 0x5300F0;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void BlockCall(IntPtr block);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void BlockArgCall(IntPtr block, int argument);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void GridUpdateCall(IntPtr lscape, uint cell, int east, int north, int fromNothing);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void SightUpdateCall(IntPtr lscape, uint sight, int blockChanged);

        /// <summary>
        /// Every block's detail checked again where it is, as the game does on entering another landblock.
        /// </summary>
        private static unsafe void RefreshBlocks(IntPtr lscape) {
            var loadPoint = *(uint*)((byte*)lscape + LScapeLoadPoint);

            if (loadPoint == 0) {
                PluginCore.LogFile("[draw-distance] no load point yet: the rings wait for the next landblock");
                return;
            }

            PluginCore.LogFile($"[draw-distance] blocks' detail checked again in place (load point 0x{loadPoint:X8}, "
                + $"{FreeMegabytes()} MB free)");
            Marshal.GetDelegateForFunctionPointer<GridUpdateCall>((IntPtr)GridUpdateAddress)(lscape, loadPoint, 0, 0, 0);
            var sight = *(uint*)((byte*)lscape + LScapeSight);
            Marshal.GetDelegateForFunctionPointer<SightUpdateCall>((IntPtr)SightUpdateAddress)(lscape, sight, 0);

            MakeBlocksObjects(lscape);
        }

        /// <summary>Whether a whole reload has been done for stale blocks since the last clean step up.</summary>
        private bool staleReloadTried;

        /// <summary>Set when a whole reload for stale blocks is to be done this pass.</summary>
        private bool staleReloadNow;

        private bool staleHoldLogged;

        /// <summary>
        /// Whether stale blocks (CountStaleBlocks) keep the scenery from stepping up this pass: the first time, a whole
        /// reload is asked for (staleReloadNow) and the step waits for it; still there after that, the scenery is held
        /// where it is (a black screen and a crash being the alternative), logged once.
        /// </summary>
        private bool StaleBlocksStopStep(IntPtr lscape) {
            var stale = CountStaleBlocks(lscape, patchedRadius);

            if (stale == 0) {
                staleReloadTried = false;
                staleHoldLogged = false;
                return false;
            }

            if (!staleReloadTried) {
                staleReloadTried = true;
                staleReloadNow = true;
                nextStepAt = DateTime.UtcNow.AddSeconds(DrawDistanceRamp.StepSeconds);
                PluginCore.LogFile($"[draw-distance] {stale} blocks outside the full-detail ring still hold objects: the "
                    + "landscape loaded again before stepping up");
                return true;
            }

            if (!staleHoldLogged) {
                staleHoldLogged = true;
                PluginCore.LogFile($"[draw-distance] {stale} blocks still hold objects after loading again: the scenery "
                    + $"stays at {patchedRadius} rather than step up onto them");
            }

            return true;
        }

        /// <summary>
        /// Blocks outside the full-detail ring that still hold objects: never so in the game's own running (a block
        /// leaving full detail has its objects destroyed, notify_change_size at 0x507525). Seen after a whole reload down from a big
        /// scenery radius (November 2026): stepping back up then found most blocks already "with" objects and made trees
        /// for none of them, the game drew next to nothing (a black screen at 120 fps) and crashed in make_plane. The
        /// game seems to keep blocks it has used, and a full-detail one came back holding its old, released objects;
        /// after a second whole reload (a plugin reload's) it was clean. So the count is logged before stepping up, and
        /// any found get another whole reload first (Apply).
        /// </summary>
        private static unsafe int CountStaleBlocks(IntPtr lscape, int fullRadius) {
            var width = *(int*)((byte*)lscape + LScapeWidth);
            var blocks = *(IntPtr**)((byte*)lscape + LScapeBlocks);

            if (blocks == null || width <= 0 || width > 2 * DrawDistanceOptions.MaxLandscapeRadius + 1) {
                return 0;
            }

            // judged by where a block is, not by what it says of itself: the first build counted blocks out of full
            // detail with objects and found none, while stepping up still made trees for 0 blocks, so the stale ones
            // still say they're in full detail. Outside the full-detail ring (the grid's middle is the load point's
            // block) a block should hold no objects at all
            var middle = width / 2;
            var stale = 0;
            var full = 0;
            var holding = 0;

            for (var i = 0; i < width * width; i++) {
                var block = blocks[i];

                if (block == IntPtr.Zero) {
                    continue;
                }

                var ring = Math.Max(Math.Abs(i % width - middle), Math.Abs(i / width - middle));
                var objects = *(int*)((byte*)block + BlockStaticCount) != 0;

                if (*(int*)((byte*)block + BlockSideCells) == 8) {
                    full++;
                }

                if (objects) {
                    holding++;
                }

                if (objects && ring > fullRadius) {
                    stale++;
                }
            }

            PluginCore.LogFile($"[draw-distance] blocks before stepping up from scenery {fullRadius}: {full} in full "
                + $"detail, {holding} holding objects, {stale} of those outside the full-detail ring");
            return stale;
        }

        /// <summary>
        /// Each block's buildings and trees made if it hasn't any yet, as grab_visible_cells does for each block, but
        /// without letting go of and grabbing the cells in sight: that, called from Decal's RenderPreUI, crashed the
        /// game (an AccessViolationException in RefreshBlocks on the second step up, November 2026). Each of the three is
        /// the game's own and safe to ask again: init_buildings (0x530AC0, none) only while the block has no buildings
        /// (+0x11C); init_static_objs (0x531780, thiscall with one argument, 0, as grab_visible_cells passes; ret 4)
        /// makes the dat's placed objects and the scenery when the block has none (+0x104), and only fits the scenery
        /// to the ground again (0x530C30) when it has; 0x5300F0 (none) sets the block's cells up once (+0xE8).
        /// The game's cells are grabbed by the game at the next change of cell, as ever.
        /// </summary>
        private static unsafe void MakeBlocksObjects(IntPtr lscape) {
            var width = *(int*)((byte*)lscape + LScapeWidth);
            var blocks = *(IntPtr**)((byte*)lscape + LScapeBlocks);

            if (blocks == null || width <= 0 || width > 2 * DrawDistanceOptions.MaxLandscapeRadius + 1) {
                return;
            }

            var initBuildings = Marshal.GetDelegateForFunctionPointer<BlockCall>((IntPtr)InitBuildingsAddress);
            var initStatics = Marshal.GetDelegateForFunctionPointer<BlockArgCall>((IntPtr)InitStaticObjsAddress);
            var initCells = Marshal.GetDelegateForFunctionPointer<BlockCall>((IntPtr)InitCellsAddress);
            var made = 0;

            for (var i = 0; i < width * width; i++) {
                var block = blocks[i];

                if (block == IntPtr.Zero || *(int*)((byte*)block + BlockSideCells) != 8) {
                    continue;
                }

                // a block with its objects already would only have its trees fitted to the ground again, for every
                // one of hundreds of blocks at each step: left alone
                if (*(int*)((byte*)block + BlockStaticCount) != 0) {
                    continue;
                }

                initBuildings(block);
                initStatics(block, 0);
                initCells(block);

                if (*(int*)((byte*)block + BlockStaticCount) != 0) {
                    made++;
                }
            }

            PluginCore.LogFile($"[draw-distance] trees made for {made} blocks newly in full detail");
        }
    }
}
