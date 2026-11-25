using System;
using System.Collections.Generic;

namespace Horizon.Feed {
    /// <summary>
    /// Where the water is round you (water reflections, Ui/DepthShading.Water.cs): Asheron's Call has no water surface of
    /// its own, only ground whose terrain is one of the water types, so a map of which terrain vertices are water says
    /// which pixels are. A terrain word's type is (word >> 2) &amp; 0x1F, and types 0x10 to 0x14 (running, standing fresh,
    /// shallow sea, shallow still sea, deep sea) are water: the client's SurfChar table, as daralet-ACE's
    /// LandblockStruct.CalcCellWater reads it. The map is one value a vertex, 255 for water, over the blocks within a
    /// radius of yours; drawn with bilinear filtering it runs smoothly from water to land across a shore cell, which is
    /// what the soft shorelines use. Kept apart from the dats so it can be tested.
    /// </summary>
    public static class WaterMap {
        public const int VerticesPerBlock = 8;

        /// <summary>Whether a terrain word is water.</summary>
        public static bool IsWater(ushort terrain) {
            var type = (terrain >> 2) & 0x1F;
            return type >= 0x10 && type <= 0x14;
        }

        /// <summary>The map's side in vertices for a radius of landblocks round yours.</summary>
        public static int Side(int radius) {
            return (radius * 2 + 1) * VerticesPerBlock + 1;
        }

        /// <summary>
        /// The map round the landblock home (0xXXYY): row by row from the south, each row west to east, a vertex every
        /// 24 meters; the home block's south-west corner is vertex (radius * 8, radius * 8). terrainOf gives a block's 81
        /// terrain words (x * 9 + y, as the cell dat has them) or null for none.
        /// </summary>
        public static byte[] Build(uint home, int radius, Func<uint, IReadOnlyList<ushort>> terrainOf) {
            var side = Side(radius);
            var map = new byte[side * side];
            var hx = (int)(home >> 8);
            var hy = (int)(home & 0xFF);

            for (var dx = -radius; dx <= radius; dx++) {
                for (var dy = -radius; dy <= radius; dy++) {
                    var bx = hx + dx;
                    var by = hy + dy;

                    if (bx < 0 || by < 0 || bx > 0xFE || by > 0xFE) {
                        continue;
                    }

                    var terrain = terrainOf((uint)(bx << 8 | by));

                    if (terrain == null || terrain.Count < 81) {
                        continue;
                    }

                    for (var vx = 0; vx <= VerticesPerBlock; vx++) {
                        for (var vy = 0; vy <= VerticesPerBlock; vy++) {
                            var gx = (dx + radius) * VerticesPerBlock + vx;
                            var gy = (dy + radius) * VerticesPerBlock + vy;

                            if (IsWater(terrain[vx * 9 + vy])) {
                                map[gy * side + gx] = 255;
                            }
                        }
                    }
                }
            }

            return map;
        }
    }
}
