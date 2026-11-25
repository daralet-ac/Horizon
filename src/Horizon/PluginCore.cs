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

        /// <summary>
        /// The id in the plugin's chat links, handed back by ChatNameClicked: any fixed number other plugins don't use
        /// ("Horz"; Daralet Toolkit's is "Dara").
        /// </summary>
        private const int ChatLinkId = 0x486F727A;

        private Settings settings;
        private GraphicsPanel panel;
        private UpdateCheck updates;
        private bool updateAnnounced;

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

                updates = new UpdateCheck(typeof(PluginCore).Assembly.GetName().Version);
                updates.Start();
                panel = new GraphicsPanel(settings) { Updates = updates };
                CoreManager.Current.CommandLineText += Core_CommandLineText;
                CoreManager.Current.RenderFrame += Core_RenderFrame;
                CoreManager.Current.ChatNameClicked += Core_ChatNameClicked;
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
                CoreManager.Current.ChatNameClicked -= Core_ChatNameClicked;
            });
            ShutdownStep(() => panel?.Dispose());
            ShutdownStep(() => updates?.Dispose());
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
                AnnounceUpdate();
            }
            catch (Exception ex) {
                Log(ex);
            }
        }

        /// <summary>Says once in chat that a newer version is out, after login: a line added before then never shows.</summary>
        private void AnnounceUpdate() {
            var newer = updates?.Newer;

            if (updateAnnounced || newer == null || CoreManager.Current.CharacterFilter.LoginStatus != 3) {
                return;
            }

            updateAnnounced = true;
            var text = $"Horizon {newer.Version.ToString(3)} is out (you have {updates.Current.ToString(3)}).";
            LogFile(text);

            try {
                // a link in chat, as UtilityBelt's update notice has: clicking it comes back as Core_ChatNameClicked
                CoreManager.Current.Actions.AddChatText($"[Horizon] {text} <Tell:IIDString:{ChatLinkId}:openurl|{newer.Url}>Download it</Tell>, then close the game and run its installer. The Update button at the top of the /horizon window opens the same page.", 1);
            }
            catch {
            }
        }

        /// <summary>A click on one of the plugin's chat links. Anyone can put this id in a tell, so only its release pages open.</summary>
        private void Core_ChatNameClicked(object sender, ChatClickInterceptEventArgs e) {
            try {
                if (e.Id != ChatLinkId) {
                    return;
                }

                e.Eat = true;
                var parts = (e.Text ?? "").Split(new[] { '|' }, 2);

                if (parts.Length == 2 && parts[0] == "openurl" && UpdateCheck.IsReleasePage(parts[1])) {
                    System.Diagnostics.Process.Start(parts[1]);
                }
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
