using System;
using System.Collections.Generic;

namespace Horizon.Feed {
    /// <summary>A stretch of frames summed up (FrameStats): what the Frame Rate window shows and compares.</summary>
    public sealed class FrameSummary {
        public int Frames { get; private set; }

        /// <summary>Frames a second over the stretch: the frames over the time they took.</summary>
        public float Fps { get; private set; }

        public float AverageMs { get; private set; }

        /// <summary>
        /// The 1% low: the frame rate of the slowest 1% of frames (the 99th percentile frame time), which is what a
        /// stutter feels like, where the average hides it.
        /// </summary>
        public float LowFps { get; private set; }

        public float WorstMs { get; private set; }

        /// <summary>Pauses of over a second (FrameStats.HitchMs), left out of the rest.</summary>
        public int Hitches { get; private set; }

        /// <summary>Whether there's enough to go by: a couple of seconds' frames.</summary>
        public bool Enough => Frames >= 30;

        internal static FrameSummary Of(List<float> times, double totalMs, int hitches) {
            var summary = new FrameSummary { Frames = times.Count, Hitches = hitches };

            if (times.Count == 0 || totalMs <= 0) {
                return summary;
            }

            times.Sort();
            var p99 = times[Math.Min(times.Count - 1, (int)Math.Ceiling(times.Count * 0.99) - 1)];
            summary.Fps = (float)(times.Count * 1000 / totalMs);
            summary.AverageMs = (float)(totalMs / times.Count);
            summary.LowFps = p99 > 0 ? 1000 / p99 : 0;
            summary.WorstMs = times[times.Count - 1];
            return summary;
        }
    }
}
