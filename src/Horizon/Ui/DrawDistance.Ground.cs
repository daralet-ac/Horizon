using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Horizon.Feed;

namespace Horizon.Ui {
    /// <summary>
    /// Ground detail (DrawDistanceOptions.GroundRadius; the user's call, November 2026: "roads are drawn from the Scenery
    /// setting too"). The game has one full-detail ring (LScape::get_block_orient, patched by Scenery): past it a block's
    /// ground is drawn from every 2nd, 4th or 8th vertex, so roads, which are marked per vertex, break up and go, and the
    /// water's edges smear (the far water the user saw). The trees came with that ring, and they're the cost. So the rings
    /// are patched out to Ground detail, and past Scenery the game makes no trees: CLandBlock::get_land_scenes (0x5311A0)
    /// asks the road check (the Scenery density hook, DrawDistance.Density.cs) of every tree, and it says "on a road" for
    /// a block further than Scenery from the load point (the LScape's +0x10, the LScape at [0x8456F0]). Buildings and the
    /// dats' placed objects don't go through it and are made as ever. The blocks left without trees are noted
    /// (skippedBlocks), for the billboards and 3D trees to stand in for (DrawDistance.Billboards.cs).
    /// <para>
    /// Walking toward one: the game makes a block's trees only when it turns full detail, and these already were. So a
    /// block now within Scenery that's noted as skipped gets them from the game's own function: get_land_scenes (thiscall,
    /// nothing else, as init_static_objs calls it at 0x531907 while the scenery flag [0x81EF08] is on), then
    /// init_static_objs (0x531780, 0) for its with-objects path, which fits them to the ground and puts each in its cell
    /// (0x511D90, 0x515C60), as MakeBlocksObjects relies on. A few blocks a look. Walking away, a block keeps its trees
    /// (nothing found to let them go) until it leaves full detail, and isn't noted, so no billboards double them.
    /// `[ground]` lines in log.txt. Untested in the game when written.
    /// </para>
    /// </summary>
    internal sealed partial class DrawDistance {
        private const uint LScapeGlobal = 0x8456F0;
        private const uint GetLandScenesAddress = 0x5311A0;
        private const uint SceneryEnabled = 0x81EF08;

        /// <summary>Blocks made a look at most, so a step across a landblock doesn't stall the game.</summary>
        private const int TreeBlocksALook = 6;

        /// <summary>How far the game's trees go, in landblocks from the load point; read as blocks load.</summary>
        private static volatile int treeRadius = DrawDistanceOptions.MaxSceneryRadius;

        /// <summary>Blocks (0xXXYY) whose last scenery the road check kept from the game; on the game's thread only.</summary>
        private static readonly HashSet<uint> skippedBlocks = new HashSet<uint>();

        /// <summary>Goes up whenever skippedBlocks changes, so the billboards know to be made again.</summary>
        private static int skippedVersion;

        private int treesMadeLogged;

        /// <summary>Whether the game may make trees in a block (its id as at the CLandBlock's +0x28), as they load.</summary>
        private static unsafe bool TreesAllowed(uint blockId) {
            var lscape = *(IntPtr*)LScapeGlobal;

            if (lscape == IntPtr.Zero) {
                return true;
            }

            var loadPoint = *(uint*)((byte*)lscape + LScapeLoadPoint);
            var key = blockId >> 16;

            if (loadPoint == 0) {
                return true;
            }

            var ring = Math.Max(Math.Abs((int)(key >> 8) - (int)(loadPoint >> 24)),
                Math.Abs((int)(key & 0xFF) - (int)((loadPoint >> 16) & 0xFF)));

            if (ring > treeRadius) {
                if (skippedBlocks.Add(key)) {
                    skippedVersion++;
                }

                return false;
            }

            if (skippedBlocks.Remove(key)) {
                skippedVersion++;
            }

            return true;
        }

        /// <summary>Whether a block (0xXXYY) has the game's trees left out.</summary>
        private static bool TreesSkipped(uint block) {
            return skippedBlocks.Contains(block);
        }

        /// <summary>The road check hooked while density or Ground detail needs it, and put back otherwise.</summary>
        private void KeepOnRoadHook(bool wanted) {
            if (wanted && densityHook == null) {
                HookOnRoad();
            }
            else if (!wanted && densityHook != null) {
                UnhookOnRoad();
            }
        }

        /// <summary>The game's trees made for blocks that have come within Scenery since they were made without.</summary>
        private unsafe void GrowTrees(IntPtr lscape) {
            if (skippedBlocks.Count == 0 || *(int*)SceneryEnabled == 0) {
                return;
            }

            var width = *(int*)((byte*)lscape + LScapeWidth);
            var blocks = *(IntPtr**)((byte*)lscape + LScapeBlocks);
            var loadPoint = *(uint*)((byte*)lscape + LScapeLoadPoint);

            if (blocks == null || loadPoint == 0 || width <= 0 || width > 2 * DrawDistanceOptions.MaxLandscapeRadius + 1) {
                return;
            }

            var middle = width / 2;
            var trees = treeRadius;
            var made = 0;

            for (var i = 0; i < width * width && made < TreeBlocksALook; i++) {
                var block = blocks[i];

                if (block == IntPtr.Zero || *(int*)((byte*)block + BlockSideCells) != 8) {
                    continue;
                }

                var ring = Math.Max(Math.Abs(i % width - middle), Math.Abs(i / width - middle));
                var key = *(uint*)((byte*)block + LandBlockId) >> 16;

                if (ring > trees || !skippedBlocks.Contains(key)) {
                    continue;
                }

                var before = *(int*)((byte*)block + BlockStaticCount);
                Marshal.GetDelegateForFunctionPointer<BlockCall>((IntPtr)GetLandScenesAddress)(block);
                Marshal.GetDelegateForFunctionPointer<BlockArgCall>((IntPtr)InitStaticObjsAddress)(block, 0);

                // the road check took it off already if it made any; a block with none (water, roads) too
                if (skippedBlocks.Remove(key)) {
                    skippedVersion++;
                }

                made++;

                if (treesMadeLogged < 20) {
                    treesMadeLogged++;
                    PluginCore.LogFile($"[ground] trees made for block {key:X4}, ring {ring}: "
                        + $"{*(int*)((byte*)block + BlockStaticCount) - before} objects");
                }
            }
        }

        /// <summary>The ring the game's trees reach now: Scenery, or less while the full-detail rings are stepping up.</summary>
        private int TreeRing => Math.Min(patchedRadius, treeRadius);
    }
}
