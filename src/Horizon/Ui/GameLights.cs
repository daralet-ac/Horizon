using System;
using System.Collections.Generic;
using Horizon.Feed;

namespace Horizon.Ui {
    /// <summary>
    /// The game's own lights near the camera (torches, lanterns, a building's lamps, spells), read where the client keeps
    /// them (the user's call, November 2026: lights and shadows from other than the sun). Render::add_static_light
    /// (0x54DFF0) and add_dynamic_light (0x54E030), cdecl (LIGHTINFO*, cell id, Frame*), both hand on to 0x54DDC0, which
    /// keeps a list of RenderLight pointers sorted nearest first, up to a most: the static lights' count at 0x8683B4 and
    /// pointers at 0x86B748, the dynamic ones' at 0x86B838 and 0x86C0D4. A RenderLight (AcClient's layout, checked
    /// against 0x54DDC0's writes): the D3DLIGHT9 first (0x68 bytes), its cell at +0x6C, then the LIGHTINFO from +0x70
    /// (its type +0x70: 0 point, 1 distant, 2 spot), its Frame from +0x74 (the object's frame and the light's offset
    /// combined, the origin at the frame's +0x34, so +0xA8, in its cell's landblock's meters), its colour +0xC0 (0 to 1),
    /// intensity +0xCC, falloff +0xD0 (the reach, meters), cone +0xD4, and the distance squared to the camera +0xD8.
    /// Untested in the game when written: `lights` (a test step) logs what's there.
    /// </summary>
    internal static unsafe class GameLights {
        private const int StaticCount = 0x8683B4;
        private const int StaticList = 0x86B748;
        private const int DynamicCount = 0x86B838;
        private const int DynamicList = 0x86C0D4;

        /// <summary>No list holds more than this, whatever its count says (a guard against reading a bad count).</summary>
        private const int MostEach = 64;

        /// <summary>The lights in both lists now, in the device's world (meters from your landblock's corner).</summary>
        public static List<GameLight> Read(uint homeLandblock) {
            var lights = new List<GameLight>();
            ReadList(lights, StaticCount, StaticList, false, homeLandblock);
            ReadList(lights, DynamicCount, DynamicList, true, homeLandblock);
            return lights;
        }

        private static void ReadList(List<GameLight> into, int countAt, int listAt, bool dynamic, uint home) {
            var count = *(int*)countAt;

            if (count <= 0 || count > MostEach) {
                return;
            }

            for (var i = 0; i < count; i++) {
                var light = *(byte**)(listAt + i * 4);

                if (light == null) {
                    continue;
                }

                var cell = *(uint*)(light + 0x6C);
                var block = cell >> 16;
                var east = *(float*)(light + 0xA8) + ((int)(block >> 8) - (int)(home >> 8)) * SceneryLayout.BlockSize;
                var north = *(float*)(light + 0xAC) + ((int)(block & 0xFF) - (int)(home & 0xFF)) * SceneryLayout.BlockSize;
                into.Add(new GameLight {
                    Dynamic = dynamic, Type = *(int*)(light + 0x70), Cell = cell,
                    East = east, North = north, Height = *(float*)(light + 0xB0),
                    Red = *(float*)(light + 0xC0), Green = *(float*)(light + 0xC4), Blue = *(float*)(light + 0xC8),
                    Intensity = *(float*)(light + 0xCC), Falloff = *(float*)(light + 0xD0),
                    Cone = *(float*)(light + 0xD4), DistanceSquared = *(float*)(light + 0xD8),
                });
            }
        }

        /// <summary>The lists as a few lines, for a test run's `lights` step.</summary>
        public static string Describe(uint home) {
            var lights = Read(home);
            var lines = new List<string> {
                $"{*(int*)StaticCount} static, {*(int*)DynamicCount} dynamic",
            };

            foreach (var l in lights) {
                lines.Add($"{(l.Dynamic ? "dynamic" : "static")} type {l.Type} cell {l.Cell:X8} at {l.East:0.0} "
                    + $"{l.North:0.0} {l.Height:0.0}, colour {l.Red:0.00} {l.Green:0.00} {l.Blue:0.00}, intensity "
                    + $"{l.Intensity:0.00}, falloff {l.Falloff:0.0}, cone {l.Cone:0.00}, {Math.Sqrt(Math.Max(0, l.DistanceSquared)):0.0} m");
            }

            return string.Join(Environment.NewLine + "    ", lines);
        }
    }
}
