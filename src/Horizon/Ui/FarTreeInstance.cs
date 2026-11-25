using System.Runtime.InteropServices;

namespace Horizon.Ui {
    /// <summary>
    /// One far tree, as the far trees' vertex shader reads it from the second stream (Shaders/fartree.vs.hlsl): where it
    /// stands in the device's world (east, height, north), its size, and the cos and sin of its turn. 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FarTreeInstance {
        public float East;
        public float Height;
        public float North;
        public float Scale;
        public float Cos;
        public float Sin;
        public float Unused1;
        public float Unused2;
    }
}
