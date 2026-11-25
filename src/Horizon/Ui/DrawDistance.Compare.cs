using System;
using System.Collections.Generic;
using System.Linq;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using Horizon.Feed;
using UtilityBelt.Service;

namespace Horizon.Ui {
    /// <summary>
    /// The scenery the game made, set against where Feed/SceneryLayout.cs says it would be (billboards stage 1, a test
    /// run's compare step): before billboards trust the layout past the scenery radius, it's checked inside it, where the
    /// real objects are.
    /// <para>
    /// The real ones: each full-detail block of the LScape's grid (+0x04 width, +0x08 the blocks) holds its objects at
    /// +0x108 (a list of CPhysicsObj, +0x104 the count), the dat's placed objects first (as many as its LandblockInfo
    /// has, the CLandBlock's +0x100, then +0x38: CLandBlock::init_static_objs and 0x530C30 count them so) and the scenery
    /// after. Each object's place is its Position at +0x48: cell id at +4, origin at +0x3C, in the block's meters. The
    /// predicted ones: the block's CellLandblock (cell dat, 0xXXYYFFFF) and the region (portal dat, 0x13000000) through
    /// SceneryLayout, at the density the loaded scenery was made with.
    /// </para>
    /// A real object counts as matched with a predicted one within MatchMeters, nearest first. Untested in the game when
    /// written.
    /// </summary>
    internal sealed partial class DrawDistance {
        private const uint RegionId = 0x13000000;
        private const int BlockLandblockInfo = 0x100;
        private const int LandblockInfoObjectCount = 0x38;
        private const int BlockObjects = 0x108;
        private const float MatchMeters = 1;

        private RegionDesc region;
        private readonly Dictionary<uint, IReadOnlyList<SceneryObject>> sceneObjects =
            new Dictionary<uint, IReadOnlyList<SceneryObject>>();

        /// <summary>
        /// The comparison in a line: blocks, real and predicted counts, how many matched, how far off in height they were.
        /// </summary>
        public unsafe string CompareScenery() {
            var smartBox = *(IntPtr*)SmartBoxPointer;
            var lscape = smartBox == IntPtr.Zero ? IntPtr.Zero : *(IntPtr*)((byte*)smartBox + SmartBoxLScape);
            var portal = UBService.PortalDat;
            var cells = UBService.CellDat;

            if (lscape == IntPtr.Zero || portal == null || cells == null) {
                return $"compare: can't ({(lscape == IntPtr.Zero ? "no landscape" : "no dat")})";
            }

            region = region ?? portal.ReadFromDat<RegionDesc>(RegionId);
            var width = *(int*)((byte*)lscape + LScapeWidth);
            var blocks = *(IntPtr**)((byte*)lscape + LScapeBlocks);
            int blocksCompared = 0, real = 0, predicted = 0, matched = 0;
            double heightError = 0;
            var worst = new List<string>();

            for (var i = 0; i < width * width; i++) {
                var block = blocks[i];

                if (block == IntPtr.Zero || *(int*)((byte*)block + BlockSideCells) != 8) {
                    continue;
                }

                var count = *(int*)((byte*)block + BlockStaticCount);
                var info = *(IntPtr*)((byte*)block + BlockLandblockInfo);
                var placedByDat = info == IntPtr.Zero ? 0 : *(int*)((byte*)info + LandblockInfoObjectCount);
                var list = *(IntPtr**)((byte*)block + BlockObjects);
                var id = *(uint*)((byte*)block + LandBlockId);
                var land = cells.ReadFromDat<CellLandblock>((id & 0xFFFF0000) | 0xFFFF);

                if (land == null || list == null) {
                    continue;
                }

                var mine = SceneryLayout.Place(id, land.Terrain, land.Height, region.LandDefs.LandHeightTable,
                    ScenesFor, id2 => ObjectsOf(portal, id2), appliedDensity, BuildingCells(cells, id));
                var theirs = new List<(float X, float Y, float Z)>();

                for (var k = placedByDat; k < count; k++) {
                    var obj = (byte*)list[k];

                    if (obj == null) {
                        continue;
                    }

                    var at = (float*)(obj + ObjectPositionOffset + PositionOriginOffset);
                    theirs.Add((at[0], at[1], at[2]));
                }

                var (hits, dz) = Match(theirs, mine);
                blocksCompared++;
                real += theirs.Count;
                predicted += mine.Count;
                matched += hits;
                heightError += dz;

                if (theirs.Count - hits > 0 && worst.Count < 3) {
                    worst.Add($"0x{id >> 16:X4}: {hits} of {theirs.Count} (predicted {mine.Count})");
                }
            }

            var line = $"compare: {blocksCompared} full-detail blocks, {real} real scenery objects, {predicted} predicted, "
                + $"{matched} matched within {MatchMeters:0} m ({(real == 0 ? 0 : 100.0 * matched / real):0.0}%)"
                + (matched > 0 ? $", height off by {heightError / matched:0.00} m on average" : "")
                + (worst.Count > 0 ? "; least matched: " + string.Join(", ", worst) : "");
            PluginCore.LogFile("[billboards] " + line);
            return line;
        }

        private const int ObjectPositionOffset = 0x48;
        private const int PositionOriginOffset = 0x3C;

        /// <summary>
        /// Each real object matched with the nearest predicted one not yet taken; the matched count and summed |dz|.
        /// </summary>
        private static (int Matched, double HeightError) Match(List<(float X, float Y, float Z)> theirs,
            List<PlacedScenery> mine) {
            var taken = new bool[mine.Count];
            var matched = 0;
            double dz = 0;

            foreach (var t in theirs) {
                var best = -1;
                var bestDistance = MatchMeters * MatchMeters;

                for (var m = 0; m < mine.Count; m++) {
                    if (taken[m]) {
                        continue;
                    }

                    var dx = mine[m].X - t.X;
                    var dy = mine[m].Y - t.Y;
                    var d = dx * dx + dy * dy;

                    if (d <= bestDistance) {
                        bestDistance = d;
                        best = m;
                    }
                }

                if (best >= 0) {
                    taken[best] = true;
                    matched++;
                    dz += Math.Abs(mine[best].Z - t.Z);
                }
            }

            return (matched, dz);
        }

        private IReadOnlyList<uint> ScenesFor(int terrainType, int sceneType) {
            var types = region.TerrainInfo.TerrainTypes;

            if (terrainType >= types.Count || sceneType >= types[terrainType].SceneTypes.Count) {
                return null;
            }

            var index = (int)types[terrainType].SceneTypes[sceneType];
            var sceneTypes = region.SceneInfo.SceneTypes;
            return index < sceneTypes.Count ? sceneTypes[index].Scenes : null;
        }

        private IReadOnlyList<SceneryObject> ObjectsOf(DatDatabase portal, uint sceneId) {
            if (sceneObjects.TryGetValue(sceneId, out var known)) {
                return known;
            }

            var scene = portal.ReadFromDat<Scene>(sceneId);
            var objects = scene?.Objects.Select(o => new SceneryObject(o.ObjId, o.BaseLoc.Origin.X, o.BaseLoc.Origin.Y,
                o.Freq, o.DisplaceX, o.DisplaceY, o.MinScale, o.MaxScale, o.MaxRotation, o.WeenieObj, o.MinSlope,
                o.MaxSlope)).ToList() ?? new List<SceneryObject>();
            sceneObjects[sceneId] = objects;
            return objects;
        }

        /// <summary>
        /// The 24 m cells of a block holding a building (cell x times 8 plus cell y), from its LandblockInfo (cell dat
        /// 0xXXYYFFFE), by each building's origin: no scenery goes there (CSortCell::has_building).
        /// </summary>
        private static HashSet<int> BuildingCells(DatDatabase cells, uint landblock) {
            var found = new HashSet<int>();
            var info = cells.ReadFromDat<LandblockInfo>((landblock & 0xFFFF0000) | 0xFFFE);

            if (info == null) {
                return found;
            }

            foreach (var building in info.Buildings) {
                found.Add(SceneryLayout.CellOf(building.Frame.Origin.X) * 8 + SceneryLayout.CellOf(building.Frame.Origin.Y));
            }

            return found;
        }
    }
}
