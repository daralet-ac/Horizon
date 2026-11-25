namespace Horizon.Feed {
    /// <summary>One step of a test run (Feed/TestScript.cs): what it is, and the line it came from.</summary>
    public sealed class TestStep {
        public TestStep(int line, string text, string kind, string field, double number, string words) {
            Line = line;
            Text = text;
            Kind = kind;
            Field = field;
            Number = number;
            Words = words;
        }

        /// <summary>Its line in the file, from 1.</summary>
        public int Line { get; }

        /// <summary>The line as written, for the results.</summary>
        public string Text { get; }

        /// <summary>wait, measure, settle, set, chat, log, restart, or bad.</summary>
        public string Kind { get; }

        /// <summary>For set: on, scenery, density, landscape, detail or far.</summary>
        public string Field { get; }

        /// <summary>Seconds for wait, measure and settle; the value for set.</summary>
        public double Number { get; }

        /// <summary>The text for chat and log; why it couldn't be read for bad.</summary>
        public string Words { get; }
    }
}
