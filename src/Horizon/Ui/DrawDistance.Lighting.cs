using System;
using System.Runtime.InteropServices;
using Microsoft.DirectX.Direct3D;

namespace Horizon.Ui {
    /// <summary>
    /// Billboards lit as the game lights its scenery. The game draws the world with Direct3D's lighting on: a sun (light 0,
    /// directional, which turns with the time of day) and an ambient colour (render state Ambient, bluish), the models'
    /// colours from their vertices. Drawn with lighting off, the billboards were the bare textures, brighter and flatter
    /// than the real trees beside them. By RenderFrame the device's light 0 has no direction left in it, so lit by it
    /// they got the ambient alone and went nearly black; so the client's own outdoor light is read from its globals
    /// and put on the device (LightLikeTheGame), with a white material and each quad's normal straight up: a tree's
    /// brightness is the ambient plus its share of the sun's strength, whatever the sun's height, the same for every
    /// side, as the game lights its outdoor objects.
    /// <para>
    /// Managed DirectX's Lights[] only echoes what was set through it, so the device's light is read with its own
    /// IDirect3DDevice9::GetLight (vtable 52) and GetLightEnable (54) for the log (`[billboards] device light` and
    /// `world light`, every 10 seconds while they're up).
    /// </para>
    /// </summary>
    internal sealed partial class DrawDistance {
        private const int GetLightSlot = 52;
        private const int GetLightEnableSlot = 54;

        /// <summary>
        /// How much of the sun a billboard takes for each unit of its strength (see LightLikeTheGame): measured against
        /// the real trees in the same places (the mean colour of their foliage in screenshots, November 2026), 0.3 of
        /// the sun at midday's strength (0.77), the share their bluer tone wanted, lit then by its height (0.92).
        /// </summary>
        private const float SunShare = 0.36f;

        private DateTime nextLightLog;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetLightCall(IntPtr device, int index, float[] light);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetLightEnableCall(IntPtr device, int index, out int enabled);

        private void LogDeviceLights(Device device) {
            if (DateTime.UtcNow < nextLightLog) {
                return;
            }

            nextLightLog = DateTime.UtcNow.AddSeconds(10);
            var text = "[billboards] device light: ";

            try {
                var state = device.RenderState;
                text += $"lighting {state.Lighting}, ambient {state.Ambient.R},{state.Ambient.G},{state.Ambient.B}, "
                    + $"fog {state.FogEnable} {state.FogStart}-{state.FogEnd}";

                for (var i = 0; i < 2; i++) {
                    var light = ReadLight(device, i, out var on);

                    if (light != null) {
                        // D3DLIGHT9: type, diffuse rgba, specular rgba, ambient rgba, position xyz, direction xyz, ...
                        text += $"; light {i} {(on ? "on" : "off")} type {light[0]} diffuse {light[1]:0.00},{light[2]:0.00},"
                            + $"{light[3]:0.00} ambient {light[9]:0.00},{light[10]:0.00},{light[11]:0.00} direction "
                            + $"{light[16]:0.00},{light[17]:0.00},{light[18]:0.00}";
                    }
                }
            }
            catch (Exception ex) {
                text += " (stopped: " + ex.Message + ")";
            }

            PluginCore.LogFile(text);
            PluginCore.LogFile("[billboards] world light: " + WorldLightText());
        }

        /// <summary>
        /// The game's outdoor light put on the device for the billboards: ambient (SmartBox::SetWorldAmbientLight's
        /// colour times level, 0x8682B0) and the sun on light 0 (its colour 0x8682BC, its vector 0x8682C8, toward the sun,
        /// x east, y north, z up), times SunShare and the vector's length: a tree's leaves face every way, so it takes
        /// some of the sun, not all.
        /// </summary>
        private static unsafe void LightLikeTheGame(Device device) {
            float F(uint at) => *(float*)at;
            int Byte(float value) => Math.Max(0, Math.Min(255, (int)(value * 255)));
            device.RenderState.Ambient = System.Drawing.Color.FromArgb(Byte(F(0x8682B0)), Byte(F(0x8682B4)),
                Byte(F(0x8682B8)));
            // the sun's strength is its vector's length, whatever its height: at dusk, with the sun low, the real trees
            // only darkened as much as the ambient did, while billboards lit by the sun's height lost a tenth more
            // (LScape::calc_object_light, the game's own rule for outdoor objects, also goes by the length)
            var x = F(0x8682C8);
            var y = F(0x8682CC);
            var z = F(0x8682D0);
            var share = SunShare * (float)Math.Sqrt(x * x + y * y + z * z);
            var sun = device.Lights[0];
            sun.Type = LightType.Directional;
            sun.DiffuseColor = new ColorValue(F(0x8682BC) * share, F(0x8682C0) * share, F(0x8682C4) * share);
            sun.AmbientColor = new ColorValue(0f, 0f, 0f);
            sun.SpecularColor = new ColorValue(0f, 0f, 0f);

            // straight down, onto normals straight up: the whole share, from any height
            sun.Direction = new Microsoft.DirectX.Vector3(0, -1, 0);
            sun.Enabled = true;
            sun.Update();
        }

        /// <summary>The client's own outdoor light, from its globals (see the class summary).</summary>
        private static unsafe string WorldLightText() {
            float F(uint at) => *(float*)at;
            var sunColour = *(uint*)0x842778;
            var ambientColour = *(uint*)0x84277C;
            return $"ambient {F(0x8682B0):0.00},{F(0x8682B4):0.00},{F(0x8682B8):0.00}; sun {F(0x8682BC):0.00},"
                + $"{F(0x8682C0):0.00},{F(0x8682C4):0.00} direction {F(0x8682C8):0.00},{F(0x8682CC):0.00},"
                + $"{F(0x8682D0):0.00}; landscape sun vector {F(0x842950):0.00},{F(0x842954):0.00},{F(0x842958):0.00}"
                + $" colour {sunColour:X8}, ambient level {F(0x842780):0.00} colour {ambientColour:X8}, "
                + $"factor {F(0x79A1E8):0.00}";
        }

        /// <summary>The device's light as D3DLIGHT9's 26 numbers (the type first, as a float), or null.</summary>
        private static unsafe float[] ReadLight(Device device, int index, out bool on) {
            on = false;
            var com = (IntPtr)device.UnmanagedComPointer;
            var vtable = *(IntPtr**)com;
            var light = new float[26];
            var getLight = Marshal.GetDelegateForFunctionPointer<GetLightCall>(vtable[GetLightSlot]);

            if (getLight(com, index, light) < 0) {
                return null;
            }

            light[0] = BitConverter.ToInt32(BitConverter.GetBytes(light[0]), 0);
            var getEnable = Marshal.GetDelegateForFunctionPointer<GetLightEnableCall>(vtable[GetLightEnableSlot]);
            on = getEnable(com, index, out var enabled) >= 0 && enabled != 0;
            return light;
        }
    }
}
