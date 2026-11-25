using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DirectX.Direct3D;

namespace Horizon.Ui {
    /// <summary>
    /// The device's shader calls, through its own vtable (CreateVertexShader 91, SetVertexShader 92,
    /// SetVertexShaderConstantF 94, CreatePixelShader 106, SetPixelShader 107, SetPixelShaderConstantF 109): Managed
    /// DirectX wants its compiler for shaders, and ours are compiled ahead (Shaders/build.cmd, embedded from
    /// Resources/shaders). Shaders aren't in a pool, so they outlive a device reset; Release them when done.
    /// </summary>
    internal static class DeviceCalls {
        private const int CreateVertexShaderSlot = 91;
        private const int SetVertexShaderSlot = 92;
        private const int SetVertexShaderConstantSlot = 94;
        private const int CreatePixelShaderSlot = 106;
        private const int SetPixelShaderSlot = 107;
        private const int SetPixelShaderConstantSlot = 109;
        private const int SetStreamSourceFreqSlot = 102;

        private static readonly System.Collections.Generic.Dictionary<int, Cached> cached =
            new System.Collections.Generic.Dictionary<int, Cached>();

        private static IntPtr cachedTable;

        private struct Cached {
            public IntPtr Function;
            public Delegate Call;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateShaderCall(IntPtr device, byte[] function, out IntPtr shader);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetShaderCall(IntPtr device, IntPtr shader);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int StreamFrequencyCall(IntPtr device, int stream, int setting);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetConstantsCall(IntPtr device, int start, float[] data, int count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReleaseCall(IntPtr thing);

        public static IntPtr VertexShader(Device device, string name) {
            return Create(device, CreateVertexShaderSlot, name);
        }

        public static IntPtr PixelShader(Device device, string name) {
            return Create(device, CreatePixelShaderSlot, name);
        }

        /// <summary>Both shaders set; zero for either puts the fixed-function pipeline back for it.</summary>
        public static void UseShaders(Device device, IntPtr vertex, IntPtr pixel) {
            Slot<SetShaderCall>(device, SetVertexShaderSlot)(Com(device), vertex);
            Slot<SetShaderCall>(device, SetPixelShaderSlot)(Com(device), pixel);
        }

        /// <summary>Vertex shader constants from register start, four floats a register.</summary>
        public static void VertexConstants(Device device, int start, float[] data) {
            Slot<SetConstantsCall>(device, SetVertexShaderConstantSlot)(Com(device), start, data, data.Length / 4);
        }

        public static void PixelConstants(Device device, int start, float[] data) {
            Slot<SetConstantsCall>(device, SetPixelShaderConstantSlot)(Com(device), start, data, data.Length / 4);
        }

        /// <summary>
        /// A stream's frequency, for hardware instancing: 0x40000000 | copies for the model's stream, 0x80000000 | 1 for the
        /// instances' own, 1 to put either back.
        /// </summary>
        public static void StreamFrequency(Device device, int stream, int setting) {
            Slot<StreamFrequencyCall>(device, SetStreamSourceFreqSlot)(Com(device), stream, setting);
        }

        public static unsafe void Release(IntPtr thing) {
            if (thing == IntPtr.Zero) {
                return;
            }

            var vtable = *(IntPtr**)thing;
            Marshal.GetDelegateForFunctionPointer<ReleaseCall>(vtable[2])(thing);
        }

        private static IntPtr Create(Device device, int slot, string name) {
            byte[] code;

            using (var stream = typeof(DeviceCalls).Assembly.GetManifestResourceStream(
                "Horizon.Resources.shaders." + name))
            using (var copy = new MemoryStream()) {
                if (stream == null) {
                    throw new InvalidOperationException("the shader " + name + " isn't in the plugin");
                }

                stream.CopyTo(copy);
                code = copy.ToArray();
            }

            if (Slot<CreateShaderCall>(device, slot)(Com(device), code, out var shader) < 0 || shader == IntPtr.Zero) {
                throw new InvalidOperationException("couldn't make the shader " + name);
            }

            return shader;
        }

        private static unsafe IntPtr Com(Device device) {
            return (IntPtr)device.UnmanagedComPointer;
        }

        /// <summary>
        /// The device's function in a slot, made a delegate once and kept: made afresh for every call, as at first, it cost
        /// the most of anything in the sun shadows' replay, two for each of thousands of draws a frame. Kept by the
        /// vtable it came from, so a new device (one vtable for every device of a kind) is looked up again.
        /// </summary>
        private static unsafe T Slot<T>(Device device, int slot) where T : Delegate {
            var vtable = *(IntPtr**)Com(device);

            if ((IntPtr)vtable != cachedTable) {
                cachedTable = (IntPtr)vtable;
                cached.Clear();
            }

            // the slot may hold something else since (the test step probe points some at itself for a frame)
            if (cached.TryGetValue(slot, out var known) && known.Function == vtable[slot]) {
                return (T)known.Call;
            }

            var made = Marshal.GetDelegateForFunctionPointer<T>(vtable[slot]);
            cached[slot] = new Cached { Function = vtable[slot], Call = made };
            return made;
        }
    }
}
