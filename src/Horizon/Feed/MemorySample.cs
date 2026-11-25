namespace Horizon.Feed {
    /// <summary>
    /// The game's memory at one moment (Ui/FrameRateWindow.Memory.cs), in megabytes: the 32-bit address space in use (its
    /// real limit: the game runs out when it can't find room, whatever the computer has), its largest free piece, the
    /// private bytes and working set Windows counts for the process, and the plugin's own managed heap.
    /// </summary>
    public struct MemorySample {
        public double At;
        public long UsedMb;
        public long TotalMb;
        public long LargestFreeMb;
        public long PrivateMb;
        public long WorkingSetMb;
        public long ManagedMb;
    }
}
