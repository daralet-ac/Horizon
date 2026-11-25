using System;
using System.Runtime.InteropServices;

namespace Horizon.Ui {
    /// <summary>
    /// Two small pieces of x86 code in front of the device's SetTransform and DrawIndexedPrimitive while sun shadows are
    /// on (ShadowCasters). A hook written in C# costs a crossing into managed code on every call the game makes, about
    /// 65 ns, some 17,000 times a frame: about 11 fps at Holtburg with no shadow map made at all (seen in the game). These
    /// do what every call needs without leaving native code: SetTransform copies a World matrix (state 256) to a buffer;
    /// DrawIndexedPrimitive, while the world is being drawn, works out how far that matrix places the draw from you, and
    /// only a draw within reach goes on to ShadowCasters' C# hook (which calls the device's own after); the rest go
    /// straight to the device's own. Both end in a jump to the function they stand in front of, so the stack and the
    /// return are the game's call's own (stdcall).
    /// <para>
    /// One page, read, write and execute: the code first, the data after (the matrix, your place, the reach squared, the
    /// recording flag, the device's own functions and the C# hook), which C# reads and writes through the pointers here.
    /// The page is never freed: a frame in progress could still be in the code, and it's 4 KB a load.
    /// </para>
    /// </summary>
    internal static unsafe class ShadowStubs {
        private const int SetTransformCode = 0x000;
        private const int DrawIndexedCode = 0x040;
        private const int WorldData = 0x200;
        private const int AreaXData = 0x240;
        private const int AreaZData = 0x244;
        private const int ReachData = 0x248;
        private const int RecordingData = 0x24C;
        private const int GameSetTransformData = 0x250;
        private const int GameDrawIndexedData = 0x254;
        private const int OursData = 0x258;

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);

        private static byte* page;

        /// <summary>The World matrix last set, 16 floats by rows.</summary>
        public static float* World => (float*)(page + WorldData);

        public static IntPtr SetTransform => (IntPtr)(page + SetTransformCode);

        public static IntPtr DrawIndexed => (IntPtr)(page + DrawIndexedCode);

        public static bool Recording {
            set => *(int*)(page + RecordingData) = value ? 1 : 0;
        }

        /// <summary>The page, made and its code written once; false if Windows wouldn't give one.</summary>
        public static bool Make() {
            if (page != null) {
                return true;
            }

            // MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE
            var made = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);

            if (made == null) {
                return false;
            }

            page = made;
            WriteSetTransform();
            WriteDrawIndexed();
            SetArea(0, 0, float.MaxValue);
            Recording = false;
            return true;
        }

        /// <summary>Where the stubs jump on to: the device's own two functions, and the C# hook for near draws.</summary>
        public static void Point(IntPtr gameSetTransform, IntPtr gameDrawIndexed, IntPtr ours) {
            *(IntPtr*)(page + GameSetTransformData) = gameSetTransform;
            *(IntPtr*)(page + GameDrawIndexedData) = gameDrawIndexed;
            *(IntPtr*)(page + OursData) = ours;
        }

        public static void SetArea(float x, float z, float reachSquared) {
            *(float*)(page + AreaXData) = x;
            *(float*)(page + AreaZData) = z;
            *(float*)(page + ReachData) = reachSquared;
        }

        private static void WriteSetTransform() {
            var code = new Writer(page + SetTransformCode);
            code.Bytes(0x8B, 0x44, 0x24, 0x08);             // mov eax,[esp+8]       the state
            code.Bytes(0x3D).Int(256);                      // cmp eax,256           World
            code.Bytes(0x75, 0x1A);                         // jne pass
            code.Bytes(0x8B, 0x44, 0x24, 0x0C);             // mov eax,[esp+0Ch]     the matrix
            code.Bytes(0x85, 0xC0);                         // test eax,eax
            code.Bytes(0x74, 0x12);                         // jz pass
            code.Bytes(0x56, 0x57);                         // push esi; push edi
            code.Bytes(0x8B, 0xF0);                         // mov esi,eax
            code.Bytes(0xBF).Address(page + WorldData);     // mov edi,World
            code.Bytes(0xB9).Int(16);                       // mov ecx,16
            code.Bytes(0xF3, 0xA5);                         // rep movsd
            code.Bytes(0x5F, 0x5E);                         // pop edi; pop esi
            code.Bytes(0xFF, 0x25).Address(page + GameSetTransformData); // pass: jmp [the device's own]
        }

        private static void WriteDrawIndexed() {
            var code = new Writer(page + DrawIndexedCode);
            code.Bytes(0x83, 0x3D).Address(page + RecordingData).Bytes(0x00); // cmp dword [Recording],0
            code.Bytes(0x74, 0x3B);                                         // je pass
            code.Bytes(0xF3, 0x0F, 0x10, 0x05).Address(page + WorldData + 48); // movss xmm0,[World x]
            code.Bytes(0xF3, 0x0F, 0x5C, 0x05).Address(page + AreaXData);    // subss xmm0,[AreaX]
            code.Bytes(0xF3, 0x0F, 0x59, 0xC0);                               // mulss xmm0,xmm0
            code.Bytes(0xF3, 0x0F, 0x10, 0x0D).Address(page + WorldData + 56); // movss xmm1,[World z]
            code.Bytes(0xF3, 0x0F, 0x5C, 0x0D).Address(page + AreaZData);    // subss xmm1,[AreaZ]
            code.Bytes(0xF3, 0x0F, 0x59, 0xC9);                               // mulss xmm1,xmm1
            code.Bytes(0xF3, 0x0F, 0x58, 0xC1);                               // addss xmm0,xmm1
            code.Bytes(0x0F, 0x2F, 0x05).Address(page + ReachData);           // comiss xmm0,[Reach]
            code.Bytes(0x77, 0x06);                                         // ja pass
            code.Bytes(0xFF, 0x25).Address(page + OursData);                // jmp [the C# hook]
            code.Bytes(0xFF, 0x25).Address(page + GameDrawIndexedData);     // pass: jmp [the device's own]
        }

        /// <summary>Bytes written one after another (a class: a struct chained by value would write through copies).</summary>
        private sealed class Writer {
            private byte* at;

            public Writer(byte* start) {
                at = start;
            }

            public Writer Bytes(params byte[] bytes) {
                foreach (var b in bytes) {
                    *at++ = b;
                }

                return this;
            }

            public Writer Int(int value) {
                *(int*)at = value;
                at += 4;
                return this;
            }

            public Writer Address(byte* address) {
                return Int((int)address);
            }
        }
    }
}
