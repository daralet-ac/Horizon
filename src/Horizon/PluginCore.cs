using System;
using System.IO;
using Decal.Adapter;
using Horizon.Feed;
using Horizon.Ui;

namespace Horizon {
    /// <summary>
    /// The Horizon plugin, started by Decal or by the hot-reload loader. Only CoreManager.Current is used, never
    /// PluginBase.Host / Core: a hot-reloaded copy isn't registered with Decal, so those are never set up for it.
    /// </summary>
    [FriendlyName("Horizon")]
    public class PluginCore : PluginBase {
        // System.IO.Path is written out in full in here: PluginBase has a Path property of its own
        private static string assemblyDirectory;

        private Settings settings;
        private GraphicsPanel panel;

        /// <summary>In minimal mode (Test Runs\minimal.txt), the only thing running.</summary>
        private TestRunner minimalRunner;

        /// <summary>
        /// The folder the dll is in, for files a player drops in by hand. Set by the loader, since a plugin loaded from bytes
        /// has no location. Everything the plugin writes goes in DataFolder.
        /// </summary>
        public static string AssemblyDirectory {
            get {
                if (assemblyDirectory == null) {
                    try {
                        assemblyDirectory = System.IO.Path.GetDirectoryName(typeof(PluginCore).Assembly.Location);
                    }
                    catch {
                        assemblyDirectory = Environment.CurrentDirectory;
                    }
                }

                return assemblyDirectory;
            }
            set {
                assemblyDirectory = value;
            }
        }

        protected override void Startup() {
            try {
                SessionLog.Start();
                CrashDumpWriter.Start();
                CrashDumpWriter.WatchMemory();
                CrashDumpWriter.HoldForDebugger();
                settings = new Settings(log: Log);

                if (settings.ImportFromToolkit(DataFolder.ToolkitSettings)) {
                    LogFile("[session] brought the graphics settings over from Daralet Toolkit");
                }

                // Test Runs\minimal.txt: nothing but the test runner, so a test run shows what the game does with the
                // plugin doing nothing else
                if (File.Exists(System.IO.Path.Combine(DataFolder.For("Test Runs"), "minimal.txt"))) {
                    LogFile("[session] minimal mode: only the test runner (Test Runs\\minimal.txt)");
                    minimalRunner = new TestRunner(settings, null, null);
                    return;
                }

                panel = new GraphicsPanel(settings);
                CoreManager.Current.CommandLineText += Core_CommandLineText;
                CoreManager.Current.RenderFrame += Core_RenderFrame;
            }
            catch (Exception ex) {
                Log(ex);
            }
        }

        /// <summary>Each step on its own: the loader swaps builds in a running game, and the old must let go of every hook.</summary>
        protected override void Shutdown() {
            ShutdownStep(() => minimalRunner?.Dispose());
            ShutdownStep(() => {
                CoreManager.Current.CommandLineText -= Core_CommandLineText;
                CoreManager.Current.RenderFrame -= Core_RenderFrame;
            });
            ShutdownStep(() => panel?.Dispose());
            ShutdownStep(() => settings?.SaveIfChanged());
            ShutdownStep(TimeOfDay.Release);
            ShutdownStep(Art.Free);
            ShutdownStep(CrashDumpWriter.Stop);
            ShutdownStep(SessionLog.End);
        }

        private static void ShutdownStep(Action step) {
            try {
                step();
            }
            catch (Exception ex) {
                Log(ex);
            }
        }

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                SessionLog.Frame(settings);
                settings.SaveIfDue();
            }
            catch (Exception ex) {
                Log(ex);
            }
        }

        /// <summary>/horizon (or @horizon) shows and hides the window.</summary>
        private void Core_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                var text = (e.Text ?? "").Trim();

                if (string.Equals(text, "/horizon", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "@horizon", StringComparison.OrdinalIgnoreCase)) {
                    e.Eat = true;
                    panel?.Toggle();
                }
            }
            catch (Exception ex) {
                Log(ex);
            }
        }

        internal static void Log(Exception ex) {
            Log(ex.ToString());
        }

        private static readonly object LogLock = new object();

        /// <summary>Writes to log.txt only, not to chat.</summary>
        internal static void LogFile(string message) {
            // logging must never be what crashes the plugin; locked, as the crash records are read on another thread
            try {
                lock (LogLock) {
                    Directory.CreateDirectory(DataFolder.Root);
                    File.AppendAllText(DataFolder.For("log.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
                }
            }
            catch {
            }
        }

        /// <summary>Writes to log.txt and to chat.</summary>
        internal static void Log(string message) {
            LogFile(message);

            try {
                CoreManager.Current.Actions.AddChatText($"[Horizon] {message}", 1);
            }
            catch {
            }
        }

        /// <summary>A note to the player in chat, in the System colour. Not logged.</summary>
        internal static void Chat(string text) {
            try {
                CoreManager.Current.Actions.AddChatText($"[Horizon] {text}", 5);
            }
            catch {
            }
        }
    }
}
