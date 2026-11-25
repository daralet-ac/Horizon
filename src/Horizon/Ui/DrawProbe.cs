using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DirectX.Direct3D;

namespace Horizon.Ui {
    /// <summary>
    /// What the game asks of Direct3D in a frame, for working out sun shadows (the test step `probe`; November 2026): a
    /// shadow map is the world drawn again from the sun, and the plugin has none of the game's meshes, so whether that
    /// can be done from the game's own draw calls depends on what they are. For one frame (from one of Decal's
    /// RenderFrames to the next) the device's draw, transform, format and stream calls are pointed here (its vtable,
    /// shared by every device of the process), counted, and passed on; then put back. The frame is split at BeginScene
    /// and at the game's see-through list (WorldPass), so the world's own calls are told from the interface's. Logged
    /// as `[probe]` lines.
    /// </summary>
    internal static unsafe class DrawProbe {
        private const int BeginSceneSlot = 41;
        private const int SetTransformSlot = 44;
        private const int DrawPrimitiveSlot = 81;
        private const int DrawIndexedPrimitiveSlot = 82;
        private const int DrawPrimitiveUpSlot = 83;
        private const int DrawIndexedPrimitiveUpSlot = 84;
        private const int SetVertexDeclarationSlot = 87;
        private const int SetFvfSlot = 89;
        private const int SetVertexShaderSlot = 92;
        private const int SetStreamSourceSlot = 100;
        private const int VertexBufferGetDescSlot = 13;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OneCall(IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int TransformCall(IntPtr device, int state, float* matrix);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DrawCall(IntPtr device, int type, int start, int count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DrawIndexedCall(IntPtr device, int type, int baseVertex, int minIndex, int vertices,
            int startIndex, int count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DrawUpCall(IntPtr device, int type, int count, IntPtr data, int stride);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DrawIndexedUpCall(IntPtr device, int type, int minIndex, int vertices, int count,
            IntPtr indices, int indexFormat, IntPtr data, int stride);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PointerCall(IntPtr device, IntPtr thing);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int FvfCall(IntPtr device, int fvf);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int StreamCall(IntPtr device, int stream, IntPtr buffer, int offset, int stride);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDescCall(IntPtr buffer, int* desc);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);

        private static readonly Dictionary<int, IntPtr> originals = new Dictionary<int, IntPtr>();

        // kept while patched, so they're never collected while the device can call them
        private static readonly List<Delegate> kept = new List<Delegate>();

        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private static readonly HashSet<string> worlds = new HashSet<string>();
        private static readonly List<string> samples = new List<string>();

        private static IntPtr* vtable;
        private static int phase;
        private static IntPtr stream0;
        private static int stream0Stride;

        private static OneCall beginScene;
        private static TransformCall setTransform;
        private static DrawCall drawPrimitive;
        private static DrawIndexedCall drawIndexed;
        private static DrawUpCall drawUp;
        private static DrawIndexedUpCall drawIndexedUp;
        private static PointerCall setDeclaration;
        private static FvfCall setFvf;
        private static PointerCall setShader;
        private static StreamCall setStream;

        /// <summary>Whether a probe is running (from one RenderFrame to the next).</summary>
        public static bool Running { get; private set; }

        /// <summary>Set by WorldPass while the plugin draws its own pieces, which aren't the game's.</summary>
        public static bool Ours { get; set; }

        /// <summary>Starts counting, from this RenderFrame to the next (call Finish then).</summary>
        public static void Start(Device device) {
            if (Running) {
                return;
            }

            counts.Clear();
            worlds.Clear();
            samples.Clear();
            phase = 0;
            vtable = *(IntPtr**)(IntPtr)device.UnmanagedComPointer;
            Patch<OneCall>(BeginSceneSlot, BeginScene, out beginScene);
            Patch<TransformCall>(SetTransformSlot, SetTransform, out setTransform);
            Patch<DrawCall>(DrawPrimitiveSlot, DrawPrimitive, out drawPrimitive);
            Patch<DrawIndexedCall>(DrawIndexedPrimitiveSlot, DrawIndexed, out drawIndexed);
            Patch<DrawUpCall>(DrawPrimitiveUpSlot, DrawUp, out drawUp);
            Patch<DrawIndexedUpCall>(DrawIndexedPrimitiveUpSlot, DrawIndexedUp, out drawIndexedUp);
            Patch<PointerCall>(SetVertexDeclarationSlot, SetDeclaration, out setDeclaration);
            Patch<FvfCall>(SetFvfSlot, SetFvf, out setFvf);
            Patch<PointerCall>(SetVertexShaderSlot, SetShader, out setShader);
            Patch<StreamCall>(SetStreamSourceSlot, SetStream, out setStream);
            Running = true;
        }

        /// <summary>The device's own calls back, and what was seen in the log.</summary>
        public static string Finish() {
            if (!Running) {
                return "no probe running";
            }

            foreach (var slot in originals) {
                Write(slot.Key, slot.Value);
            }

            originals.Clear();
            kept.Clear();
            Running = false;

            var lines = counts.OrderBy(c => c.Key).Select(c => $"{c.Key} {c.Value}").ToList();
            PluginCore.LogFile("[probe] " + string.Join("; ", lines));
            PluginCore.LogFile($"[probe] {worlds.Count} different World matrices in the world pass");

            foreach (var sample in samples) {
                PluginCore.LogFile("[probe] " + sample);
            }

            return $"{lines.Count} kinds of call, {worlds.Count} World matrices (see log.txt)";
        }

        /// <summary>From here on, the game's see-through list and after (WorldPass).</summary>
        public static void SeeThrough() {
            if (Running) {
                phase = 2;
            }
        }

        private static void Patch<T>(int slot, T ours, out T original) where T : Delegate {
            originals[slot] = vtable[slot];
            original = Marshal.GetDelegateForFunctionPointer<T>(vtable[slot]);
            kept.Add(ours);
            Write(slot, Marshal.GetFunctionPointerForDelegate(ours));
        }

        private static void Write(int slot, IntPtr value) {
            var at = (IntPtr)(vtable + slot);
            VirtualProtect(at, (UIntPtr)4, 0x04, out var old);
            vtable[slot] = value;
            VirtualProtect(at, (UIntPtr)4, old, out _);
        }

        private static string Phase => Ours ? "ours" : phase == 0 ? "before-scene" : phase == 1 ? "world" : "see-through";

        private static void Count(string what, int add = 1) {
            var key = Phase + " " + what;
            counts[key] = (counts.TryGetValue(key, out var n) ? n : 0) + add;
        }

        private static void Sample(string line) {
            if (samples.Count < 40) {
                samples.Add(Phase + " " + line);
            }
        }

        private static int BeginScene(IntPtr device) {
            phase = 1;
            Count("BeginScene");
            return beginScene(device);
        }

        private static int SetTransform(IntPtr device, int state, float* matrix) {
            Count($"SetTransform {state}");

            if (state == 256 && phase == 1 && !Ours && matrix != null) {
                worlds.Add($"{matrix[12]:0.0},{matrix[13]:0.0},{matrix[14]:0.0}");
            }

            if ((state == 2 || state == 3) && matrix != null) {
                Sample($"transform {state}: {matrix[0]:0.###} {matrix[5]:0.###} {matrix[10]:0.###} "
                    + $"{matrix[11]:0.###} {matrix[12]:0.#} {matrix[13]:0.#} {matrix[14]:0.#} {matrix[15]:0.###}");
            }

            return setTransform(device, state, matrix);
        }

        private static int DrawPrimitive(IntPtr device, int type, int start, int count) {
            Count("DrawPrimitive");
            Count("DrawPrimitive triangles", count);
            Count("DrawPrimitive from " + PoolOf(stream0));
            return drawPrimitive(device, type, start, count);
        }

        private static int DrawIndexed(IntPtr device, int type, int baseVertex, int minIndex, int vertices,
            int startIndex, int count) {
            Count("DrawIndexedPrimitive");
            Count("DrawIndexedPrimitive triangles", count);
            Count("DrawIndexedPrimitive from " + PoolOf(stream0));
            return drawIndexed(device, type, baseVertex, minIndex, vertices, startIndex, count);
        }

        private static int DrawUp(IntPtr device, int type, int count, IntPtr data, int stride) {
            Count("DrawPrimitiveUP");
            Count("DrawPrimitiveUP triangles", count);
            return drawUp(device, type, count, data, stride);
        }

        private static int DrawIndexedUp(IntPtr device, int type, int minIndex, int vertices, int count, IntPtr indices,
            int indexFormat, IntPtr data, int stride) {
            Count("DrawIndexedPrimitiveUP");
            Count("DrawIndexedPrimitiveUP triangles", count);
            Count($"DrawIndexedPrimitiveUP stride {stride}");
            return drawIndexedUp(device, type, minIndex, vertices, count, indices, indexFormat, data, stride);
        }

        private static int SetDeclaration(IntPtr device, IntPtr declaration) {
            Count("SetVertexDeclaration");
            return setDeclaration(device, declaration);
        }

        private static int SetFvf(IntPtr device, int fvf) {
            Count($"SetFVF {fvf:X}");
            return setFvf(device, fvf);
        }

        private static int SetShader(IntPtr device, IntPtr shader) {
            Count(shader == IntPtr.Zero ? "SetVertexShader none" : "SetVertexShader a shader");
            return setShader(device, shader);
        }

        private static int SetStream(IntPtr device, int stream, IntPtr buffer, int offset, int stride) {
            if (stream == 0) {
                stream0 = buffer;
                stream0Stride = stride;
            }

            Count($"SetStreamSource {stream} stride {stride}");
            return setStream(device, stream, buffer, offset, stride);
        }

        /// <summary>The vertex buffer's pool and whether it's dynamic, from its description.</summary>
        private static string PoolOf(IntPtr buffer) {
            if (buffer == IntPtr.Zero) {
                return "no buffer";
            }

            try {
                var table = *(IntPtr**)buffer;
                var desc = stackalloc int[6];
                Marshal.GetDelegateForFunctionPointer<GetDescCall>(table[VertexBufferGetDescSlot])(buffer, desc);
                var pool = desc[3] == 0 ? "default" : desc[3] == 1 ? "managed" : desc[3] == 2 ? "system" : "pool " + desc[3];
                return pool + ((desc[2] & 0x200) != 0 ? " dynamic" : "") + $" stride {stream0Stride}";
            }
            catch {
                return "unknown buffer";
            }
        }
    }
}
