using System;

namespace Horizon.Feed {
    /// <summary>
    /// Which scenery objects a density under 100% keeps (DrawDistanceOptions.SceneryDensity, Ui/DrawDistance.Density.cs):
    /// decided by the landblock and the object's place in it alone, so a block loaded again keeps the same trees and
    /// thinning never makes them flicker or move. Kept apart from the hook so it can be tested.
    /// </summary>
    public static class SceneryDensity {
        /// <summary>
        /// Whether the object the game would put here (landblock id, x and y in the block's meters) is kept. A salt of its
        /// own gives another choice of the same share (a thinning by distance once used it, taken out in November 2026).
        /// </summary>
        public static bool Keeps(uint landblock, float x, float y, int percent, uint salt = 0) {
            if (percent >= 100) {
                return true;
            }

            if (percent <= 0) {
                return false;
            }

            var hash = Mix(salt ^ (landblock >> 16) * 0x9E3779B1u
                ^ (uint)BitConverter.ToInt32(BitConverter.GetBytes(x), 0) * 0x85EBCA77u
                ^ (uint)BitConverter.ToInt32(BitConverter.GetBytes(y), 0) * 0xC2B2AE3Du);
            return hash % 100 < (uint)percent;
        }

        /// <summary>A finishing mix (murmur3's), so nearby places don't come out alike.</summary>
        private static uint Mix(uint h) {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
