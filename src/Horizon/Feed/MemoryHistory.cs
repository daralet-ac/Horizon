using System;
using System.Collections.Generic;

namespace Horizon.Feed {
    /// <summary>
    /// The game's memory over the session (the user's call, November 2026: "the client's memory leak is my top concern"):
    /// a sample every few seconds (MemorySample), two hours kept, and how fast the address space in use grows over the
    /// last so many minutes, in megabytes a minute, from a straight line fitted through the samples (one spike, a
    /// landscape loading, doesn't decide it). Kept apart from the reading so it can be tested.
    /// </summary>
    public sealed class MemoryHistory {
        public const double KeepSeconds = 2 * 60 * 60;

        private readonly List<MemorySample> samples = new List<MemorySample>();

        public IReadOnlyList<MemorySample> Samples => samples;

        public MemorySample? Latest => samples.Count == 0 ? (MemorySample?)null : samples[samples.Count - 1];

        /// <summary>Seconds from the first sample to the last.</summary>
        public double Span => samples.Count < 2 ? 0 : samples[samples.Count - 1].At - samples[0].At;

        public void Add(MemorySample sample) {
            samples.Add(sample);

            while (samples.Count > 0 && samples[0].At < sample.At - KeepSeconds) {
                samples.RemoveAt(0);
            }
        }

        public void Clear() {
            samples.Clear();
        }

        /// <summary>
        /// How fast the address space in use grows, megabytes a minute, over the last so many seconds (all of them for
        /// 0 or less); null with under a minute of samples to go by.
        /// </summary>
        public double? GrowthPerMinute(double seconds) {
            if (samples.Count < 2) {
                return null;
            }

            var end = samples[samples.Count - 1].At;
            var from = seconds > 0 ? end - seconds : double.MinValue;
            double n = 0, sumX = 0, sumY = 0, sumXY = 0, sumXX = 0, first = double.MaxValue;

            foreach (var s in samples) {
                if (s.At < from) {
                    continue;
                }

                var x = s.At / 60;
                n++;
                sumX += x;
                sumY += s.UsedMb;
                sumXY += x * s.UsedMb;
                sumXX += x * x;
                first = Math.Min(first, s.At);
            }

            var denominator = n * sumXX - sumX * sumX;

            if (n < 3 || end - first < 60 || Math.Abs(denominator) < 1e-9) {
                return null;
            }

            return (n * sumXY - sumX * sumY) / denominator;
        }

        /// <summary>
        /// About how many minutes until only so many megabytes are left free at a rate, or null when it isn't growing.
        /// </summary>
        public static double? MinutesUntil(MemorySample now, double perMinute, long leaveFreeMb) {
            if (perMinute <= 0.05) {
                return null;
            }

            var room = now.TotalMb - now.UsedMb - leaveFreeMb;
            return room <= 0 ? 0 : room / perMinute;
        }
    }
}
