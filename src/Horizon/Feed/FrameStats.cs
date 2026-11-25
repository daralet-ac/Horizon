using System;
using System.Collections.Generic;

namespace Horizon.Feed {
    /// <summary>
    /// The game's frame times, for the Frame Rate window (Ui/FrameRateWindow.cs): the last two minutes of frames, with
    /// marks where draw distance settings changed, so a setting can be judged by the frame rate before and after it.
    /// Times are seconds from any fixed start; kept apart from Decal so it can be tested.
    /// </summary>
    public sealed class FrameStats {
        /// <summary>How much is kept.</summary>
        public const double KeepSeconds = 120;

        /// <summary>
        /// A frame longer than this is a pause (alt-tab, a loading screen, the landscape loading again), not a frame:
        /// counted as a hitch and left out of the rates, or one would drag a whole stretch's average down.
        /// </summary>
        public const double HitchMs = 1000;

        /// <summary>After a mark, this long is left out of "after": a draw distance change loads the landscape again.</summary>
        public const double SettleSeconds = 3;

        private readonly Queue<(double At, float Ms)> frames = new Queue<(double, float)>();
        private readonly List<(double At, string Label)> marks = new List<(double, string)>();
        private readonly Queue<double> hitches = new Queue<double>();

        public IReadOnlyList<(double At, string Label)> Marks => marks;

        /// <summary>A frame that ended at this time, having taken this many milliseconds.</summary>
        public void Add(double at, double ms) {
            if (ms <= 0) {
                return;
            }

            if (ms > HitchMs) {
                hitches.Enqueue(at);
            }
            else {
                frames.Enqueue((at, (float)ms));
            }

            var oldest = at - KeepSeconds;

            while (frames.Count > 0 && frames.Peek().At < oldest) {
                frames.Dequeue();
            }

            while (hitches.Count > 0 && hitches.Peek() < oldest) {
                hitches.Dequeue();
            }

            marks.RemoveAll(m => m.At < oldest);
        }

        /// <summary>Notes a change at this time ("Scenery 5"...), shown on the graph and compared before and after.</summary>
        public void Mark(double at, string label) {
            marks.Add((at, label));
        }

        public void Clear() {
            frames.Clear();
            marks.Clear();
            hitches.Clear();
        }

        /// <summary>The frames that ended from (inclusive) to (exclusive), summed up.</summary>
        public FrameSummary Between(double from, double to) {
            var times = new List<float>();
            double total = 0;

            foreach (var (at, ms) in frames) {
                if (at >= from && at < to) {
                    times.Add(ms);
                    total += ms;
                }
            }

            var pauses = 0;

            foreach (var at in hitches) {
                if (at >= from && at < to) {
                    pauses++;
                }
            }

            return FrameSummary.Of(times, total, pauses);
        }

        /// <summary>The last so many seconds up to now.</summary>
        public FrameSummary Last(double now, double seconds) {
            return Between(now - seconds, now + 1);
        }

        /// <summary>
        /// A mark's before and after, for the log: up to so many seconds before it (from the mark before it, its reload
        /// left out), and as many after it, from SettleSeconds on.
        /// </summary>
        public (FrameSummary Before, FrameSummary After) Around(double at, double previousAt, double seconds) {
            var start = Math.Max(at - seconds, previousAt + SettleSeconds);
            return (Between(start, at), Between(at + SettleSeconds, at + SettleSeconds + seconds));
        }

        /// <summary>
        /// The last mark's before and after: up to this many seconds before it (stopping at the mark before, whose
        /// reload would count), and from SettleSeconds after it to now (up to this many seconds). Null with no mark.
        /// </summary>
        public (FrameSummary Before, FrameSummary After, string Label, double At)? AroundLastMark(double now,
            double seconds) {
            if (marks.Count == 0) {
                return null;
            }

            var (at, label) = marks[marks.Count - 1];
            var start = at - seconds;

            if (marks.Count > 1) {
                start = Math.Max(start, marks[marks.Count - 2].At + SettleSeconds);
            }

            var afterFrom = at + SettleSeconds;
            var after = Between(Math.Max(afterFrom, now - seconds), now + 1);
            return (Between(start, at), after, label, at);
        }

        /// <summary>
        /// The frame rate in each of so many equal stretches of the last so many seconds, oldest first; 0 where there
        /// were no frames (a pause), for the graph.
        /// </summary>
        public float[] Buckets(double now, double seconds, int count) {
            var sums = new double[count];
            var counts = new int[count];
            var start = now - seconds;
            var width = seconds / count;

            foreach (var (at, ms) in frames) {
                var i = (int)Math.Floor((at - start) / width);

                if (i < 0 || i >= count) {
                    continue;
                }

                sums[i] += ms;
                counts[i]++;
            }

            var fps = new float[count];

            for (var i = 0; i < count; i++) {
                fps[i] = sums[i] > 0 ? (float)(counts[i] * 1000 / sums[i]) : 0;
            }

            return fps;
        }
    }
}
