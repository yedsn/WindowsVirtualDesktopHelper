using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using WindowsVirtualDesktopHelper.VirtualDesktopAPI;
using WindowsVirtualDesktopHelper.WindowsHotKeyAPI;
using System.Drawing.Text;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Security.Policy;

namespace WindowsVirtualDesktopHelper {

	class App {

		#region Public Properties

		public static App Instance;
		public IVirtualDesktopManager VDAPI = null;
		public string CurrentVDDisplayName = null;
		public uint CurrentVDDisplayNumber = 0;
		public int CurrentVDDisplayCount = 1;
		public SettingsForm SettingsForm;
		public WindowOverviewForm WindowOverviewForm;
		public DesktopLayoutSnapshotForm DesktopLayoutSnapshotForm;
		public AppForm AppForm;
		public DesktopLayoutSnapshotService DesktopLayoutSnapshots;
		public string CurrentSystemThemeName = null;
		public static string DetectedVDImplementation = null;

		#endregion

		#region Internal Structs

		internal class HotKeyAction {
			public string HotKeyAndAction;
			public string HotKey;
			public string Action;
			public Keys Keys;
			public ModifierKeys Modifiers;
		}

		#endregion

		#region Private/Internal Properties

		private KeyboardHook KeyboardHooksJumpToDesktop = null;
		private KeyboardHook _keyboardHooks = null;
		private List<HotKeyAction> _keyboardHooksHotKeysAndActions = new List<HotKeyAction>(); // the registered hotkey actions
		private Dictionary<int, IntPtr> VDDToLastFocusedWin = new Dictionary<int, IntPtr>();
		public IntPtr LastForegroundhWnd = IntPtr.Zero; //TODO: this should be private
		public List<string> FGWindowHistory = new List<string>(); //TODO: this should be private // needed to detect if Task View was open
		private List<int> _desktopNumberHistory = new List<int>(); // stores a list of most recent desktop numbers used

		#endregion

		#region Constructor

		public App() {
			// Set the app instance global
			App.Instance = this;

			// Settings
			{
				Util.Logging.WriteLine("Using config file(s):\r\n   "+string.Join("\r\n   ", Settings.GetUsedConfigFiles()));
				Util.Logging.WriteLine("Config: \r\n   "+Settings.GetSettingsAsString().Replace("\n", "\r\n   "));
			}

			// Test global error form:
			//throw new Exception("test exception!");

			// Load the implementation
			try {
				this.LoadVDAPI();
				this.LoadVDDisplayInfo();
				//throw new Exception("Test error", new Exception("Test inner error"));
			} catch (Exception e) {
				throw e;
			}

			// Load theme
			this.CurrentSystemThemeName = this.GetSystemThemeName();

			this.CurrentVDDisplayCount = this.GetVDDisplayCount();

			// Create the app form, which acts as our ui main thread (we need such a main thread form for many of the win api calls)
			this.AppForm = new AppForm();
			this.DesktopLayoutSnapshots = new DesktopLayoutSnapshotService(this);

			// Create settings form
			this.SettingsForm = new SettingsForm();

			// Hot keys
			this.SetupHotKeys();

		}

		#endregion

		#region Virtual Desktop Methods

		public void LoadVDAPI() {
			App.DetectedVDImplementation = VirtualDesktopAPI.Loader.GetImplementationForOS();
			this.VDAPI = VirtualDesktopAPI.Loader.LoadImplementationWithFallback(App.DetectedVDImplementation);
		}

		public void LoadVDDisplayInfo() {
			try {
				this.CurrentVDDisplayNumber = this.GetVDDisplayNumber(true);
			} catch (Exception e) {
				throw new Exception("LoadVDDisplayInfo: could not get current display number: " + e.Message, e);
			}
			try {
				this.CurrentVDDisplayName = this.GetVDDisplayName(true);
			} catch (Exception e) {
				throw new Exception("LoadVDDisplayInfo: could not get current display name: " + e.Message, e);
			}
		}

		public uint GetVDDisplayNumber(bool throwException) {
			try {
				return this.VDAPI.Current();
			} catch (Exception e) {
				if (throwException) throw new Exception("GetVDDisplayNumber: could not get current display number: " + e.Message, e);
				else return 0;
			}
		}

		public int GetVDDisplayCount() {
			return this.VDAPI.DisplayCount();
		}

		public string GetVDDisplayName(bool throwException) {
			try {
				return this.VDAPI.CurrentDisplayName();
			} catch (Exception e) {
				if (throwException) throw new Exception("GetVDDisplayName: could not get current display number: " + e.Message, e);
				else return "Unknown";
			}
		}

		public void MonitorVDisplayCount() {
			var thread = new Thread(new ThreadStart(_MonitorVDDisplayCount));
			thread.Start();
		}
		private void _MonitorVDDisplayCount() {
			while(true) {
				try {
					var newCurrentVDDisplayCount = this.GetVDDisplayCount();
					if(newCurrentVDDisplayCount != CurrentVDDisplayCount) {
						CurrentVDDisplayCount = newCurrentVDDisplayCount;
						this.UIUpdateIcons();
						//Debug.WriteLine("Update Count: " + Thread.CurrentThread.ManagedThreadId);
					}
					System.Threading.Thread.Sleep(100);
				} catch(Exception e) {
					Util.Logging.WriteLine("App: Error: MonitorVDDisplayCount: " + e.Message);
					System.Threading.Thread.Sleep(1000);
				}
			}
		}

		public void MonitorVDSwitch() {
			var thread = new Thread(new ThreadStart(_MonitorVDSwitch));
			//var thread2 = new Thread(new ThreadStart(delegate { UpdateSettingFormSafe(); }));
			thread.Start();
			//thread2.Start();
		}
		private void _MonitorVDSwitch() {
			while(true) {
				try {
					// Throw on error, so that a failure is not mistaken for desktop number 0 and
					// so that a stale API connection can be detected and recovered in the catch below
					var newVDDisplayNumber = this.GetVDDisplayNumber(true);
					_vdApiFailureCount = 0;
					if(newVDDisplayNumber != this.CurrentVDDisplayNumber) {
						this.CurrentVDDisplayName = this.GetVDDisplayName(false);
						this.CurrentVDDisplayNumber = newVDDisplayNumber;
						//Util.Logging.WriteLine("Switched to " + this.CurrentVDDisplayNumber);
						VDSwitchedSafe();
					} else {
						//storeLastWinFocused();
					}
					System.Threading.Thread.Sleep(100);
				} catch(Exception e) {
					Util.Logging.WriteLine("App: Error: MonitorVDSwitch: " + e.Message);
					_tryReconnectVDAPI();
					System.Threading.Thread.Sleep(1000);
				}
			}
		}

		private int _vdApiFailureCount = 0;

		private void _tryReconnectVDAPI() {
			// The virtual desktop API lives in the explorer.exe process; when explorer crashes or
			// restarts, the cached COM objects become stale and every call fails from then on.
			// After a few consecutive failures we reconnect, otherwise the app stays broken until
			// it is manually restarted.
			_vdApiFailureCount++;
			if(_vdApiFailureCount < 5) return;
			_vdApiFailureCount = 0;
			try {
				Util.Logging.WriteLine("App: MonitorVDSwitch: reconnecting to the virtual desktop API...");
				this.VDAPI.Reconnect();
				this.VDAPI.Current(); // test the connection
				Util.Logging.WriteLine("App: MonitorVDSwitch: reconnect successful");
			} catch(Exception e) {
				Util.Logging.WriteLine("App: MonitorVDSwitch: could not reconnect to the virtual desktop API: " + e.Message);
			}
		}

		public void VDSwitchedSafe() {
			// Make sure we run on the main thread
			if(this.AppForm.InvokeRequired) {
				Action safeAction = delegate { VDSwitchedSafe(); };
				this.AppForm.Invoke(safeAction);
			} else {
				// Update the active tray layout.
				this.UIUpdateIcons();
				// Show notification overlay
				if(Settings.GetBool("feature.showDesktopSwitchOverlay")) {
					this.AppForm.Invoke((Action)(() => {
						SwitchNotificationForm.CloseAllNotifications(this.AppForm);
						if(Settings.GetBool("feature.showDesktopSwitchOverlay.showOnAllMonitors")) {
							for(var i = 0; i < Screen.AllScreens.Length; i++) {
								var form = new SwitchNotificationForm(i);
								form.LabelText = this.CurrentVDDisplayName;
								form.DisplayTimeMS = Settings.GetInt("feature.showDesktopSwitchOverlay.duration");
								form.Show();
							}
						} else {
							var form = new SwitchNotificationForm();
							form.LabelText = this.CurrentVDDisplayName;
							form.DisplayTimeMS = Settings.GetInt("feature.showDesktopSwitchOverlay.duration");
							form.Show();
						}
					}));
				}
				// Update permanent overlay
				UpdateStatusOverlayWindows();
				// Restore focus
				if(Settings.GetBool("feature.restorePreviousWindowFocus")) {
					try {
						_restorePrevWinFocus();
					} catch(Exception e) {
						Util.Logging.WriteLine("App: Error: SwitchDesktopForward (restorePrevWinFocus()): " + e.Message);
					}
				}
				// Log this desktop number in _desktopNumberHistory, only keeping the last 20
				if(_desktopNumberHistory.Count == 0 || _desktopNumberHistory[_desktopNumberHistory.Count - 1] != this.CurrentVDDisplayNumber) {
					_desktopNumberHistory.Add((int)this.CurrentVDDisplayNumber);
				}
				if(_desktopNumberHistory.Count > 20) {
					_desktopNumberHistory.RemoveRange(0, _desktopNumberHistory.Count - 20);
				}
			}
		}

		public void SwitchDesktopBackward() {
			// We try the virtual desktop implementation API, but fallback to shortcut keys if it fails...
			try {
				VDAPI.SwitchBackward();
			} catch(Exception e) {
				Util.Logging.WriteLine("App: Error: SwitchDesktopBackward (VDAPI.SwitchBackward()): " + e.Message);
				Util.OS.DesktopBackwardBySimulatingShortcutKey();
			}
		}

		public void SwitchDesktopForward() {
			// We try the virtual desktop implementation API, but fallback to shortcut keys if it fails...
			try {
				VDAPI.SwitchForward();
			} catch(Exception e) {
				Util.Logging.WriteLine("App: Error: SwitchDesktopForward (VDAPI.SwitchForward()): " + e.Message);
				Util.OS.DesktopForwardBySimulatingShortcutKey();
			}
		}

		public void OpenTaskView() {
			if(this.FGWindowHistory.Contains("Task View")) return;

			new Thread(() => {
				Thread.Sleep(100);
				Util.OS.OpenTaskView();
			}).Start();
		}

		public void OpenConfiguredWindowManager() {
			if(GetTrayWindowManagerMode() == "built-in") ShowWindowOverview();
			else OpenTaskView();
		}

		public string GetTrayWindowManagerMode() {
			return Settings.GetString("feature.iconTray.windowManager", "system") == "built-in" ? "built-in" : "system";
		}

		public void SwitchToDesktop(int number) {
			// Explicitly store the last focused window
			try {
				_storeLastWinFocused();
			} catch(Exception e) {
				Util.Logging.WriteLine("App: Error: SwitchToDesktop (storeLastWinFocused()): " + e.Message);
			}
			// We try the virtual desktop implementation API
			try {
				VDAPI.SwitchToDesktop(number);
			} catch(Exception e) {
				Util.Logging.WriteLine("App: Error: SwitchToDesktop (VDAPI.SwitchToDesktop(number)): " + e.Message);
				return;
			}
		}

		public void SwitchToPreviousDesktop() {
			// Switch to the previous desktop number from _desktopNumberHistory which is not the current desktop number
			for(var i = _desktopNumberHistory.Count - 1; i >= 0; i--) {
				if(_desktopNumberHistory[i] != this.CurrentVDDisplayNumber) {
					this.SwitchToDesktop(_desktopNumberHistory[i]);
					return;
				}
			}
		}

		public List<WindowOverviewItem> GetWindowOverviewSnapshot() {
			var items = new List<WindowOverviewItem>();
			var desktopIndices = VirtualDesktopRegistry.GetDesktopIndices();
			using(var desktopLookup = new WindowDesktopLookup()) foreach(var window in Util.WindowEnumerator.GetApplicationWindows()) {
				var desktopIndex = -1;
				var isShownOnAllDesktops = false;
				Guid desktopId;
				if(desktopLookup.TryGetWindowDesktopId(window.Handle, out desktopId)) {
					if(!desktopIndices.TryGetValue(desktopId, out desktopIndex)) isShownOnAllDesktops = desktopId != Guid.Empty;
				}
				items.Add(new WindowOverviewItem(window, desktopIndex, isShownOnAllDesktops));
			}
			return items;
		}

		public int GetWindowOverviewDesktopCount() {
			return Math.Max(1, Math.Max(CurrentVDDisplayCount, VirtualDesktopRegistry.GetDesktopCount()));
		}

		public List<string> GetWindowOverviewDesktopNames() {
			return VirtualDesktopRegistry.GetDesktopNames();
		}

		public string ActivateOverviewWindow(WindowOverviewItem item) {
			if(item == null || !Util.WindowEnumerator.IsWindow(item.Window.Handle)) return "The selected window is no longer available.";
			try {
				if(item.DesktopIndex >= 0 && item.DesktopIndex != (int)CurrentVDDisplayNumber) {
					SwitchToDesktop(item.DesktopIndex);
					Thread.Sleep(150);
				}
				return Util.WindowEnumerator.TryActivate(item.Window.Handle) ? "Window activated." : "Windows did not allow that window to be activated.";
			} catch(Exception e) {
				Util.Logging.WriteLine("App: ActivateOverviewWindow: " + e.Message);
				return "Could not activate the selected window.";
			}
		}

		public string CloseOverviewWindow(WindowOverviewItem item) {
			if(item == null || !Util.WindowEnumerator.IsWindow(item.Window.Handle)) return "The selected window is no longer available.";
			return Util.WindowEnumerator.TryClose(item.Window.Handle) ? "Close request sent." : "Could not send a close request to the selected window.";
		}

		public string MoveOverviewWindow(WindowOverviewItem item, int targetDesktopIndex) {
			if(item == null || !Util.WindowEnumerator.IsWindow(item.Window.Handle)) return "The selected window is no longer available.";
			if(item.IsShownOnAllDesktops) return "Windows shown on all desktops cannot be moved.";
			if(item.DesktopIndex == targetDesktopIndex) return "The window is already on that desktop.";
			Guid desktopId;
			if(!VirtualDesktopRegistry.TryGetDesktopId(targetDesktopIndex, out desktopId)) return "The target desktop is no longer available.";
			try {
				using(var desktopLookup = new WindowDesktopLookup()) {
					string error;
					if(desktopLookup.TryMoveWindowToDesktop(item.Window.Handle, desktopId, out error)) return "Window moved to Desktop " + (targetDesktopIndex + 1) + ".";
					Util.Logging.WriteLine("App: MoveOverviewWindow: public move failed: " + error);
				}
				var privateMover = VDAPI as IWindowDesktopMover;
				if(privateMover == null) return "Windows did not allow that window to be moved.";
				privateMover.MoveWindowToDesktop(item.Window.Handle, targetDesktopIndex);
				return "Window moved to Desktop " + (targetDesktopIndex + 1) + ".";
			} catch(Exception e) {
				Util.Logging.WriteLine("App: MoveOverviewWindow: " + e.Message);
				return "Could not move the selected window.";
			}
		}

		public void EnsureDesktopCount(int count) {
			count = Math.Max(1, count);
			var attempts = 0;
			while(VirtualDesktopRegistry.GetDesktopCount() < count && attempts++ < count * 10) {
				VDAPI.CreateDesktop();
				Thread.Sleep(100);
			}
			if(VirtualDesktopRegistry.GetDesktopCount() < count) throw new InvalidOperationException("Windows did not create the required virtual desktops.");
			CurrentVDDisplayCount = GetVDDisplayCount();
			if(AppForm != null) UIUpdateIcons();
		}

		public bool TryMoveWindowToDesktop(IntPtr windowHandle, int targetDesktopIndex, out string error) {
			error = null;
			Guid desktopId;
			if(!VirtualDesktopRegistry.TryGetDesktopId(targetDesktopIndex, out desktopId)) { error = "The target desktop is no longer available."; return false; }
			try {
				using(var desktopLookup = new WindowDesktopLookup()) {
					if(desktopLookup.TryMoveWindowToDesktop(windowHandle, desktopId, out error)) return true;
				}
				var privateMover = VDAPI as IWindowDesktopMover;
				if(privateMover == null) { error = "Windows did not allow that window to be moved."; return false; }
				privateMover.MoveWindowToDesktop(windowHandle, targetDesktopIndex);
				return true;
			} catch(Exception e) {
				Util.Logging.WriteLine("App: TryMoveWindowToDesktop: " + e.Message);
				error = "Could not move the window: " + e.Message;
				return false;
			}
		}

		public void UpdateStatusOverlayWindows() {
			if(Settings.GetBool("feature.showDesktopStatusOverlay")) {
				this.AppForm.Invoke((Action)(() => {
					OverlayForm.CloseAllNotifications(this.AppForm);
					if(Settings.GetBool("feature.showDesktopStatusOverlay.showOnAllMonitors")) {
						for(var i = 0; i < Screen.AllScreens.Length; i++) {
							var form = new OverlayForm(i);
							form.LabelText = this.CurrentVDDisplayName;
							form.Show();
						}
					} else {
						var form = new OverlayForm();
						form.LabelText = this.CurrentVDDisplayName;
						form.Show();
					}
				}));
			} else {
				OverlayForm.CloseAllNotifications(this.AppForm);
			}
		}

		#endregion

		#region Window Methods

		public void MonitorFGWindowName() {
			var thread = new Thread(new ThreadStart(_MonitorFGWindowName));
			thread.Start();
		}

		private void _MonitorFGWindowName() {
			while(true) {
				try {
					var fgWindowName = Util.OS.GetForegroundWindowName();
					FGWindowHistory.Add(fgWindowName);
					var maxHistory = 20;
					if(FGWindowHistory.Count > maxHistory) {
						FGWindowHistory.RemoveRange(0, FGWindowHistory.Count - maxHistory);
					}
					if(LastForegroundhWnd == IntPtr.Zero) {
						LastForegroundhWnd = Util.OS.GetFolderViewHandle();
					}
					System.Threading.Thread.Sleep(20);
				} catch(Exception e) {
					Util.Logging.WriteLine("App: Error: MonitorFGWindowName: " + e.Message);
					System.Threading.Thread.Sleep(1000);
				}
			}
		}


		public void MonitorFocusedWindow() {
			var thread = new Thread(new ThreadStart(_monitorFocusedWindow));
			thread.Start();
		}

		private void _monitorFocusedWindow() {
			while(true) {
				try {
					_storeLastWinFocused();
					System.Threading.Thread.Sleep(200);
				} catch(Exception e) {
					Util.Logging.WriteLine("App: Error: _monitorFocusedWindow: " + e.Message);
					System.Threading.Thread.Sleep(1000);
				}
			}
		}

		private void _storeLastWinFocused() {
			IntPtr hWnd = Util.OS.GetForegroundWindow();
			if(hWnd != IntPtr.Zero) {
				var fgWindowName = Util.OS.GetForegroundWindowName();
				var fgWindowType = Util.OS.GetHandleWndType(hWnd);
				if(fgWindowType == "Shell_TrayWnd") return; // we ignore the icon tray, since this takes the focus away when we click the prev/next arrows
				var displayNumber = (int)this.GetVDDisplayNumber(false);
				if(VDDToLastFocusedWin.ContainsKey(displayNumber)) {
					VDDToLastFocusedWin[displayNumber] = hWnd;
				} else {
					VDDToLastFocusedWin.Add(displayNumber, hWnd);
				}
				//Console.WriteLine($"store: display {displayNumber} hwnd {hWnd} ({fgWindowType})");
			}
		}

		private void _restorePrevWinFocus() {
			var displayNumber = (int)this.GetVDDisplayNumber(false);
			if(VDDToLastFocusedWin.ContainsKey(displayNumber)) {
				IntPtr lastWindowHandle = VDDToLastFocusedWin[displayNumber];
				if(Util.OS.IsWindow(lastWindowHandle)) {
					Util.OS.SetForegroundWindow(lastWindowHandle);
					//Console.WriteLine("restore: "+ displayNumber + " "+ lastWindowHandle);
				}
			}
		}

		#endregion

		#region Actions API

		//TODO: expose this API to the command line in a --noGUI mode

		public string RunAction(string action) {
			// The following actions are supported:
			// - DesktopForward
			// - DesktopBackward
			// - PreviousDesktop
			// - TaskView
			// - Desktop1...Desktop99
			try {

				action = action.Trim().ToLower();
				if(action == "desktopforward") {
					this.SwitchDesktopForward();
					return null;
				} else if(action == "desktopbackward") {
					this.SwitchDesktopBackward();
					return null;
				} else if(action == "previousdesktop") {
					this.SwitchToPreviousDesktop();
					return null;
				} else if(action == "taskview") {
					this.OpenConfiguredWindowManager();
					return null;
				} else if(action.StartsWith("desktop")) {
					var desktopNumber = 0;
					if(int.TryParse(action.Replace("desktop", ""), out desktopNumber)) {
						this.SwitchToDesktop(desktopNumber - 1);
						return null;
					} else {
						throw new Exception("invalid desktop number");
					}
				} else {
					throw new Exception("invalid action");
				}

			} catch(Exception e) {
				Util.Logging.WriteLine($"RunAction: error with action \"{action}\": {e.Message}");
				return $"error with action \"{action}\": {e.Message}";
			}
		}

		#endregion

		#region Hot Keys

		public void SetupHotKeys() {
			// Clear old hooks
			if(this._keyboardHooks != null) {
				this._keyboardHooks.Dispose();
				this._keyboardHooks = null;
			}

			// Compile a list of all hotkeys and their actions
			// A hotkey defintion and command looks like the following samples:
			// "Alt + I = Desktop1"
			// "Alt + O = Desktop2"
			// "Alt + P = Desktop3"
			// "Ctrl + Alt + Left = DesktopLeft"
			var hotkeys = new List<string>();

			// Compile from custom configs
			// hotkeys.myCustomKey1: "Alt + I = Desktop1"
			// hotkeys.myCustomKey2: "Alt + O = Desktop2"
			// hotkeys.myCustomKey3: "Alt + P = Desktop3"
			// hotkeys.myCustomKey4: "Ctrl + Alt + Left = DesktopLeft"
			foreach(var setting in Settings.GetKeys()) {
				if(setting.StartsWith("hotkeys.")) {
					hotkeys.Add(Settings.GetString(setting));
				}
			}

			// Compile from features
			if(Settings.GetBool("feature.useHotKeyToJumpToDesktopNumber")) {
				var hotKey = Settings.GetString("feature.useHotKeyToJumpToDesktopNumber.hotkey");
				if(hotKey != null && hotKey != "") {
					for(var i = 1; i <= 9; i++) {
						hotkeys.Add($"{hotKey} + D{i} = Desktop{i}");
						hotkeys.Add($"{hotKey} + NumPad{i} = Desktop{i}");
					}
				}
			}
			if(Settings.GetBool("feature.useHotKeyToJumpToPreviousDesktop")) {
				var hotKey = Settings.GetString("feature.useHotKeyToJumpToPreviousDesktop.hotkey");
				if(hotKey != null && hotKey != "") {
					hotkeys.Add($"{hotKey} = PreviousDesktop");
				}
			}
			if(Settings.GetBool("feature.useHotKeyToSwitchDesktopForward")) {
				var hotKey = Settings.GetString("feature.useHotKeyToSwitchDesktopForward.hotkey");
				if(hotKey != null && hotKey != "") {
					hotkeys.Add($"{hotKey} = DesktopForward");
				}
			}
			if(Settings.GetBool("feature.useHotKeyToSwitchDesktopBackward")) {
				var hotKey = Settings.GetString("feature.useHotKeyToSwitchDesktopBackward.hotkey");
				if(hotKey != null && hotKey != "") {
					hotkeys.Add($"{hotKey} = DesktopBackward");
				}
			}
			if(Settings.GetBool("feature.useHotKeyToOpenTaskView")) {
				var hotKey = Settings.GetString("feature.useHotKeyToOpenTaskView.hotkey");
				if(hotKey != null && hotKey != "") {
					hotkeys.Add($"{hotKey} = TaskView");
				}
			}

			// Parse all hotkeys to cached HotKeyAction structs
			_keyboardHooksHotKeysAndActions = new List<HotKeyAction>();
			foreach(var hotkeyAndAction in hotkeys) {
				 {
					// Init HotKeyAction struct
					var hotkeyAction = new HotKeyAction();
					hotkeyAction.HotKeyAndAction = hotkeyAndAction;
					hotkeyAction.HotKey = hotkeyAndAction.Split('=').First().Trim();
					hotkeyAction.Action = hotkeyAndAction.Split('=').Last().Trim();
					// Parse the keys to the Keys and KeyModifiers enums
					{
						var keys = hotkeyAction.HotKey.Split('+');
						//TODO: explode some special keys like Number to D0..D9, NumPad0..NumPad9
						Keys hookKeys = 0;
						ModifierKeys hookModifierKeys = 0;
						foreach(var key in keys) {
							var keyCleaned = key.Trim();
							// Normalize some key names
							if(keyCleaned.ToLower() == "ctrl") keyCleaned = "control";
							// Support Oem keys
							// OemSemicolon = 0xBA,
							// Oem1 = 0xBA,
							// Oemplus = 0xBB,
							// Oemcomma = 0xBC,
							// OemMinus = 0xBD,
							// OemPeriod = 0xBE,
							// OemQuestion = 0xBF,
							// Oem2 = 0xBF,
							// Oemtilde = 0xC0,
							// Oem3 = 0xC0,
							// OemOpenBrackets = 0xDB,
							// Oem4 = 0xDB,
							// OemPipe = 0xDC,
							// Oem5 = 0xDC,
							// OemCloseBrackets = 0xDD,
							// Oem6 = 0xDD,
							// OemQuotes = 0xDE,
							// Oem7 = 0xDE,
							// Oem8 = 0xDF,
							// OemBackslash = 0xE2,
							// Oem102 = 0xE2,
							if(keyCleaned.ToLower() == "semicolon") keyCleaned = "OemSemicolon";
							if(keyCleaned.ToLower() == "plus") keyCleaned = "Oemplus";
							if(keyCleaned.ToLower() == "comma") keyCleaned = "Oemcomma";
							if(keyCleaned.ToLower() == "minus") keyCleaned = "OemMinus";
							if(keyCleaned.ToLower() == "period") keyCleaned = "OemPeriod";
							if(keyCleaned.ToLower() == "question") keyCleaned = "OemQuestion";
							if(keyCleaned.ToLower() == "tilde") keyCleaned = "Oemtilde";
							if(keyCleaned.ToLower() == "openbrackets") keyCleaned = "OemOpenBrackets";
							if(keyCleaned.ToLower() == "pipe") keyCleaned = "OemPipe";
							if(keyCleaned.ToLower() == "closebrackets") keyCleaned = "OemCloseBrackets";
							if(keyCleaned.ToLower() == "quotes") keyCleaned = "OemQuotes";
							if(keyCleaned.ToLower() == "backslash") keyCleaned = "OemBackslash";
							if(keyCleaned.ToLower() == "clear") keyCleaned = "OemClear";
							ModifierKeys keyModifier;
							var keyModifierValid = Enum.TryParse<ModifierKeys>(keyCleaned, true, out keyModifier);
							if(keyModifierValid) {
								// Add to hookModifierKeys enum
								hookModifierKeys = hookModifierKeys | keyModifier; // can be multiple modifiers
							} else {
								Keys keyKey;
								var keyKeyValid = Enum.TryParse<Keys>(keyCleaned, true, out keyKey);
								if(keyKeyValid) {
									hookKeys = keyKey; // only a single key is supported
								}
							}
						}
						hotkeyAction.Keys = hookKeys;
						hotkeyAction.Modifiers = hookModifierKeys;
					}
					// Validate: must have at least one key and one modifier
					bool isValid = true;
					if(hotkeyAction.Modifiers == 0) {
						Util.Logging.WriteLine($"SetupHotKeys: Invalid hotkey {hotkeyAction.HotKeyAndAction}, a hotkey must have at least one modifier (Ctrl,Alt,Shift,Win)");
						isValid = false;
					}
					if(hotkeyAction.Keys == 0 || hotkeyAction.Keys == Keys.None) {
						Util.Logging.WriteLine($"SetupHotKeys: Invalid hotkey {hotkeyAction.HotKeyAndAction}, a hotkey must have at least one key");
						isValid = false;
					}
					// Register
					if(isValid) _keyboardHooksHotKeysAndActions.Add(hotkeyAction);
				}
			}

			// Init the keyboard hooks
			this._keyboardHooks = new KeyboardHook();
			this._keyboardHooks.KeyPressed += new EventHandler<KeyPressedEventArgs>(_hotKeyPressed);

			// Register all the hotkeys in _keyboardHooksHotKeysAndActions
			// Note: registration can fail, for example when another application already owns the
			// hotkey or when a combination is listed twice - this must not crash the app, so we
			// log the failure and keep the remaining hotkeys working
			foreach(var hotkeyAction in _keyboardHooksHotKeysAndActions) {
				try {
					this._keyboardHooks.RegisterHotKey(hotkeyAction.Modifiers, hotkeyAction.Keys);
				} catch(Exception e) {
					Util.Logging.WriteLine($"SetupHotKeys: could not register hotkey {hotkeyAction.HotKeyAndAction}: {e.Message}");
				}
			}


		}

		private void _hotKeyPressed(object sender, KeyPressedEventArgs e) {
			var keys = e.Key;
			var modifiers = e.Modifier;
			// Find matching HotKeyAction
			foreach(var hotkeyAction in _keyboardHooksHotKeysAndActions) {
				if(hotkeyAction.Keys == keys && hotkeyAction.Modifiers == modifiers) {
					RunAction(hotkeyAction.Action);
				}
			}
		}

		#endregion

		#region OS/System Theme

		public void MonitorSystemThemeSwitch() {
			var thread = new Thread(new ThreadStart(_MonitorSystemThemeSwitch));
			thread.Start();
		}

		private void _MonitorSystemThemeSwitch() {
			while (true) {
				try {
					var newSystemThemeName = this.GetSystemThemeName();
					if (newSystemThemeName != this.CurrentSystemThemeName) {
						this.CurrentSystemThemeName = newSystemThemeName;
						ThemeSwitched();
					}
					System.Threading.Thread.Sleep(1000);
				} catch (Exception e) {
					Util.Logging.WriteLine("App: Error: " + e.Message);
					System.Threading.Thread.Sleep(5000);
				}
			}
		}

		public void ThemeSwitched() {
			this.UIUpdateIcons();
		}

		public string GetSystemThemeName() {
			var themeSetting = Settings.GetString("general.theme");
			if(themeSetting == "auto") {
				return Util.OS.IsSystemLightThemeModeEnabled() == true ? "light" : "dark";
			} else if(themeSetting == "light") {
				return "light";
			} else if(themeSetting == "dark") {
				return "dark";
			} else {
				throw new Exception("invalid theme setting general.theme: " + themeSetting);
			}

		}

		#endregion

		#region Startup

		public void EnableStartupWithWindows() {
			// https://stackoverflow.com/questions/674628/how-do-i-set-a-program-to-launch-at-startup
			try {
				Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
				key.SetValue(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title, Application.ExecutablePath);
			} catch (Exception e) {
				throw new Exception("EnableStartupWithWindows: could not set registry value: " + e.Message);
			}
		}

		public void DisableStartupWithWindows() {
			// https://stackoverflow.com/questions/674628/how-do-i-set-a-program-to-launch-at-startup
			try {
				Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
				key.DeleteValue(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title, false);
			} catch (Exception e) {
				throw new Exception("EnableStartupWithWindows: could not delete registry value: " + e.Message);
			}
		}

		#endregion

		#region UI

		public void UIUpdate() {
			// Icons
			UIUpdateIcons();
		}

		public void UIUpdateIcons() {
			if(this.AppForm.InvokeRequired) {
				try {
					this.AppForm.BeginInvoke((Action)(() => UIUpdateIcons()));
				} catch(InvalidOperationException) { }
				return;
			}

			var theme = App.Instance.CurrentSystemThemeName;
			if(GetTrayDesktopDisplayMode() == "all-desktops") {
				this.AppForm.notifyIconName.Visible = false;
				this.AppForm.notifyIconNumber.Visible = false;
				this.AppForm.notifyIconPrev.Visible = false;
				this.AppForm.notifyIconNext.Visible = false;
				this.AppForm.UpdateDesktopNotifyIcons(theme, App.Instance.CurrentVDDisplayCount, App.Instance.CurrentVDDisplayNumber, this.AppForm.DeviceDpi);
			} else {
				this.AppForm.DisposeDesktopNotifyIcons();
				// Set current display icons
				UIUpdateIconForVDDisplayNumber(theme, App.Instance.CurrentVDDisplayNumber, App.Instance.CurrentVDDisplayName);
				UIUpdateIconForVDDisplayName(theme, App.Instance.CurrentVDDisplayName);
				UIUpdateNextPrevIconVisibility(theme);
				// Visibility by feature
				this.AppForm.notifyIconName.Visible = Settings.GetBool("feature.showDesktopNameInIconTray");
				this.AppForm.notifyIconNumber.Visible = Settings.GetBool("feature.showDesktopNumberInIconTray");
			}
		}

		public string GetTrayDesktopDisplayMode() {
			var mode = Settings.GetString("feature.iconTray.desktopDisplayMode", "navigation");
			return mode == "all-desktops" ? mode : "navigation";
		}


		public void UIUpdateIconForVDDisplayNumber(string theme, uint number, string name) {
			number++;
			this.AppForm.notifyIconNumber.Icon = Util.Icons.GenerateNotificationIcon(number.ToString(), theme, this.AppForm.DeviceDpi, false);
		}

		public void UIUpdateIconForVDDisplayName(string theme, string name) {
			var nameToShow = name;
			if(nameToShow == null) nameToShow = "";
			if(nameToShow.Length > 1) nameToShow = new StringInfo(nameToShow).SubstringByTextElements(0, 1);
			this.AppForm.notifyIconName.Icon = Util.Icons.GenerateNotificationIcon(nameToShow, theme, this.AppForm.DeviceDpi, false);
		}

		public void UIUpdateNextPrevIconVisibility(string theme) {
			if(Settings.GetBool("feature.showPrevNextIcons")) {
				int count = App.Instance.CurrentVDDisplayCount - 1;
				var prevChar = Settings.GetString("feature.showPrevNextIcons.prevChar");
				var nextChar = Settings.GetString("feature.showPrevNextIcons.nextChar");
				var hasNextDesktop = count != 0 && App.Instance.CurrentVDDisplayNumber != count;
				var hasPrevDesktop = App.Instance.CurrentVDDisplayNumber != 0;
				// Update prev/next icons
				this.AppForm.notifyIconPrev.Icon = Util.Icons.GenerateNotificationIcon(prevChar, theme, this.AppForm.DeviceDpi, true, hasPrevDesktop ? 1.0f : Settings.GetDouble("theme.icons.disabledOpacity"));
				this.AppForm.notifyIconNext.Icon = Util.Icons.GenerateNotificationIcon(nextChar, theme, this.AppForm.DeviceDpi, true, hasNextDesktop ? 1.0f : Settings.GetDouble("theme.icons.disabledOpacity"));
				// Show or hide?
				if(Settings.GetBool("feature.showPrevNextIcons.automaticallyHidePrevNextOnBounds")) {
					this.AppForm.notifyIconNext.Visible = hasNextDesktop;
					this.AppForm.notifyIconPrev.Visible = hasPrevDesktop;
				} else {
					this.AppForm.notifyIconNext.Visible = true;
					this.AppForm.notifyIconPrev.Visible = true;
				}
			} else {
				this.AppForm.notifyIconNext.Visible = false;
				this.AppForm.notifyIconPrev.Visible = false;
			}
		}

		#endregion

		#region Forms and Windows

		public void ShowAbout() {
			this.AppForm.Invoke((Action)(() => {
				var form = new AboutForm();
				form.Show();
			}));
		}

		public void ShowSettings() {
			this.SettingsForm.Show();
		}

		public void ShowWindowOverview() {
			if(WindowOverviewForm == null || WindowOverviewForm.IsDisposed) WindowOverviewForm = new WindowOverviewForm();
			WindowOverviewForm.UpdateWindowIcon(CurrentSystemThemeName, AppForm.DeviceDpi);
			if(!WindowOverviewForm.Visible) WindowOverviewForm.Show();
			WindowOverviewForm.BringToFront();
			WindowOverviewForm.Activate();
			WindowOverviewForm.RefreshSnapshot();
			WindowOverviewForm.FocusSearchBox();
		}

		public void ShowDesktopLayoutSnapshots() {
			if(DesktopLayoutSnapshotForm == null || DesktopLayoutSnapshotForm.IsDisposed) DesktopLayoutSnapshotForm = new DesktopLayoutSnapshotForm();
			if(!DesktopLayoutSnapshotForm.Visible) DesktopLayoutSnapshotForm.Show();
			DesktopLayoutSnapshotForm.BringToFront();
			DesktopLayoutSnapshotForm.Activate();
			DesktopLayoutSnapshotForm.RefreshSnapshots();
		}

		public void CreateDesktopLayoutSnapshot() {
			ShowDesktopLayoutSnapshots();
			DesktopLayoutSnapshotForm.CreateNewSnapshot();
		}

		public void RestoreMostRecentDesktopLayoutSnapshot() {
			ShowDesktopLayoutSnapshots();
			DesktopLayoutSnapshotForm.RestoreMostRecent();
		}

		public void ShowSplash() {
			if(Settings.GetBool("feature.showSplashScreen")) {
				if(Settings.GetBool("feature.showDesktopSwitchOverlay")) {
					this.AppForm.Invoke((Action)(() => {
						var form = new SwitchNotificationForm();
						form.DisplayTimeMS = Settings.GetInt("feature.showSplashScreen.duration");
						form.LabelText = Settings.GetString("feature.showSplashScreen.text");
						form.Show();
					}));
				}
			}
		}

		#endregion

		#region Misc

		public void OpenURL(string url) {
			url = url.Replace("&", "^&"); //TODO: is this really needed?
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}

		public void Exit() {
			if(this.AppForm != null && !this.AppForm.IsDisposed) {
				this.AppForm.DisposeDesktopNotifyIcons();
			}
			Application.Exit();
			System.Environment.Exit(0);
		}

		public void OpenEmailContact() {
			App.Instance.OpenURL("mailto:dan@dankrusi.com");
		}

		public void OpenAboutPage() {
			App.Instance.OpenURL("https://github.com/dankrusi/WindowsVirtualDesktopHelper");
		}

		public void OpenDonatePage() {
			App.Instance.OpenURL("https://www.paypal.com/donate/?hosted_button_id=BG5FYMAHFG9V6");
		}

		#endregion

	}
}
