using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.FocusedHotkeys
{
    class FocusedHotkeysComponent : LogicComponent
    {
        public override string ComponentName => "Focused Hotkeys";

        public bool Enabled { get; protected set; }
        public FocusedHotkeysSettings Settings { get; set; }

        const int UPDATE_INTERVAL = 50; // minimum interval between updates in milliseconds

        bool _prevHotkeysEnabled;
        readonly Stopwatch _stopWatch;

        public StringBuilder titleBuilder = new StringBuilder(256);

        public FocusedHotkeysComponent(LiveSplitState state)
        {
            Settings = new FocusedHotkeysSettings();
            Enabled = true;
            _prevHotkeysEnabled = state.Settings.GlobalHotkeysEnabled;
            _stopWatch = Stopwatch.StartNew();
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetWindowText(int hWnd, StringBuilder title, int size);

        public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            if (_stopWatch.ElapsedMilliseconds < UPDATE_INTERVAL)
                return;
            _stopWatch.Restart();

            if (Settings.ProgramList.Count > 0)
            {
                if (IsFocused())
                {
                    if (Settings.AllowFocusedToggle)
                    {
                        // toggle focused hotkeys along global hotkeys if they are manually toggled during focus
                        if (state.Settings.GlobalHotkeysEnabled != _prevHotkeysEnabled)
                            Enabled = state.Settings.GlobalHotkeysEnabled;
                        else if (Enabled)
                            state.Settings.GlobalHotkeysEnabled = true;
                    }
                    else
                        state.Settings.GlobalHotkeysEnabled = Enabled = true; // reset Enabled to true if toggle setting is off
                }
                else // global hotkeys are never allowed without focus
                    state.Settings.GlobalHotkeysEnabled = false;
            }
            _prevHotkeysEnabled = state.Settings.GlobalHotkeysEnabled;
        }

        bool IsFocused()
        {
            StringComparison comparer = Settings.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            var window = GetForegroundWindow();
            GetWindowThreadProcessId(window, out uint pid);
            var process = Process.GetProcessById((int)pid);
            var result = GetWindowText(window.ToInt32(), titleBuilder, 256);
            var title = titleBuilder.ToString();

            return Settings.ProgramList.Any(s =>
            {
                switch(s.Type)
                {
                    case TitleType.ProcessName:
                        return s.Title.Equals(process.ProcessName, comparer);

                    case TitleType.WindowTitle:
                        /*
                         * If the result is zero, then the call to GetWindowTextW() failed, and we should just return false.
                         * -Ted
                         */
                        return result != 0 && s.Title.Equals(title, comparer);
                        
                    default:
                        return false;
                }
            });
        }

        public override XmlNode GetSettings(XmlDocument document) => Settings.GetSettings(document);

        public override Control GetSettingsControl(LayoutMode mode) => Settings;

        public override void SetSettings(XmlNode settings) => Settings.SetSettings(settings);

        public override void Dispose()
        {
            Settings.Dispose();
        }
    }
}
