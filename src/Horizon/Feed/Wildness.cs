using System;
using System.Collections.Generic;

namespace Horizon.Feed {
    /// <summary>
    /// How wild a place is, 0 kept to 1 wild (the user's call, November 2026: short, well-kept grass near towns, taller
    /// and denser out in the wilds): by its distance from the nearest town or outpost in the map's places
    /// (Map/Locations.cs, GoArrow's locations.xml). Within a town's kept reach it's 0, past its wild reach 1, and
    /// smoothly between, so there's no line where a town's lawn ends; outposts keep a smaller patch. Places are in map
    /// units (EW east, NS north, a unit 240 meters, -102 to 102), the world in meters from its south-west corner, a
    /// landblock's x * 192 plus its own meters (Map/Navigation.MapCoords, backwards). Kept apart so it can be tested.
    /// </summary>
    public sealed class Wildness {
        public const float TownKept = 250;
        public const float TownWild = 700;
        public const float OutpostKept = 90;
        public const float OutpostWild = 350;

        private readonly List<(float X, float Y, float Kept, float Wild)> places =
            new List<(float, float, float, float)>();

        /// <summary>Towns and outposts from places of (type, east, north) in map units; every other type is left out.</summary>
        public Wildness(IEnumerable<(string Type, float EastWest, float NorthSouth)> mapPlaces) {
            foreach (var place in mapPlaces) {
                var town = string.Equals(place.Type, "Town", StringComparison.OrdinalIgnoreCase);
                var outpost = string.Equals(place.Type, "Outpost", StringComparison.OrdinalIgnoreCase);

                if (!town && !outpost) {
                    continue;
                }

                places.Add(((place.EastWest + 102) * 240, (place.NorthSouth + 102) * 240,
                    town ? TownKept : OutpostKept, town ? TownWild : OutpostWild));
            }
        }

        public int Places => places.Count;

        /// <summary>The world meters of a place in a landblock (0xXXYY) at its own meters.</summary>
        public static (float X, float Y) WorldMeters(uint block, float x, float y) {
            return ((block >> 8) * 192 + x, (block & 0xFF) * 192 + y);
        }

        /// <summary>0 kept to 1 wild at world meters; wild everywhere with no places known.</summary>
        public float At(float worldX, float worldY) {
            var wild = 1f;

            foreach (var place in places) {
                var dx = worldX - place.X;
                var dy = worldY - place.Y;

                // most places are far: a cheap check before the square root
                if (Math.Abs(dx) > place.Wild || Math.Abs(dy) > place.Wild) {
                    continue;
                }

                var distance = (float)Math.Sqrt(dx * dx + dy * dy);
                wild = Math.Min(wild, SmoothStep(place.Kept, place.Wild, distance));
            }

            return wild;
        }

        private static float SmoothStep(float from, float to, float value) {
            var t = Math.Max(0, Math.Min(1, (value - from) / (to - from)));
            return t * t * (3 - 2 * t);
        }
    }
}
