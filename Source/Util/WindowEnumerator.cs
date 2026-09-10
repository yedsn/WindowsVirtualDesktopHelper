using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsVirtualDesktopHelper.Util {
	public sealed class ApplicationWindow {
		public IntPtr Handle { get; private set; }
		public int ProcessId { get; private set; }
		public string ProcessName { get; private set; }
		public string ProcessPath { get; private set; }
		public DateTime? ProcessStartTimeUtc { get; private set; }
		public string ClassName { get; private set; }
		public string AppUserModelId { get; private set; }
		public string Title { get; private set; }
		public Icon Icon { get; private set; }

		internal ApplicationWindow(IntPtr handle, int processId, string processName, string processPath, DateTime? processStartTimeUtc, string className, string appUserModelId, string title, Icon icon) {
			Handle = handle;
			ProcessId = processId;
			ProcessName = processName;
			ProcessPath = processPath;
			ProcessStartTimeUtc = processStartTimeUtc;
			ClassName = className;
			AppUserModelId = appUserModelId;
			Title = title;
			Icon = icon;
		}
	}

	public static class WindowEnumerator {
		private const int GWL_EXSTYLE = -20;
		private const long WS_EX_TOOLWINDOW = 0x00000080L;
		private const int SW_RESTORE = 9;
		private const uint WM_CLOSE = 0x0010;
		private const uint WM_GETICON = 0x007F;
		private const int ICON_SMALL2 = 2;
		private const int GCLP_HICONSM = -34;
		private const uint SMTO_ABORTIFHUNG = 0x0002;

		private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern IntPtr GetWindow(IntPtr hWnd, uint command);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetApplicationUserModelId(IntPtr processHandle, ref uint applicationUserModelIdLength, StringBuilder applicationUserModelId);

		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

		[DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
		private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

		[DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
		private static extern int GetWindowLong32(IntPtr hWnd, int index);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool ShowWindow(IntPtr hWnd, int command);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsIconic(IntPtr hWnd);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

		[DllImport("user32.dll", EntryPoint = "GetClassLongPtr", SetLastError = true)]
		private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int index);

		[DllImport("user32.dll", EntryPoint = "GetClassLong", SetLastError = true)]
		private static extern uint GetClassLong32(IntPtr hWnd, int index);

		public static List<ApplicationWindow> GetApplicationWindows() {
			var windows = new List<ApplicationWindow>();
			EnumWindows((hWnd, lParam) => {
				ApplicationWindow window;
				if(TryGetApplicationWindow(hWnd, out window)) windows.Add(window);
				return true;
			}, IntPtr.Zero);
			return windows;
		}

		public static bool TryActivate(IntPtr hWnd) {
			if(!IsWindow(hWnd)) return false;
			if(IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
			return SetForegroundWindow(hWnd);
		}

		public static bool TryClose(IntPtr hWnd) {
			return IsWindow(hWnd) && PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
		}

		private static bool TryGetApplicationWindow(IntPtr hWnd, out ApplicationWindow window) {
			window = null;
			// Windows cloaks windows on non-current virtual desktops. They are the
			// primary reason for this overview, so they must remain in the snapshot.
			if(!IsWindowVisible(hWnd) || GetWindow(hWnd, 4) != IntPtr.Zero) return false;
			if((GetExtendedStyle(hWnd) & WS_EX_TOOLWINDOW) != 0 || IsShellWindow(hWnd)) return false;

			uint processId;
			GetWindowThreadProcessId(hWnd, out processId);
			if(processId == 0 || processId == (uint)Process.GetCurrentProcess().Id) return false;

			var processName = "Unknown application";
			string processPath = null;
			DateTime? processStartTimeUtc = null;
			string appUserModelId = null;
			try {
				using(var process = Process.GetProcessById((int)processId)) {
					processName = process.ProcessName;
					try { processPath = process.MainModule.FileName; } catch { }
					try { processStartTimeUtc = process.StartTime.ToUniversalTime(); } catch { }
					try { appUserModelId = GetProcessAppUserModelId(process.Handle); } catch { }
				}
			} catch { }

			window = new ApplicationWindow(hWnd, (int)processId, processName, processPath, processStartTimeUtc, GetClassName(hWnd), appUserModelId, GetTitle(hWnd), GetSmallIcon(hWnd));
			return true;
		}

		private static string GetClassName(IntPtr hWnd) {
			var className = new StringBuilder(256);
			return GetClassName(hWnd, className, className.Capacity) == 0 ? null : className.ToString();
		}

		private static string GetProcessAppUserModelId(IntPtr processHandle) {
			uint length = 0;
			var result = GetApplicationUserModelId(processHandle, ref length, null);
			if(result != 122 || length == 0) return null;
			var appUserModelId = new StringBuilder((int)length);
			return GetApplicationUserModelId(processHandle, ref length, appUserModelId) == 0 ? appUserModelId.ToString() : null;
		}

		private static long GetExtendedStyle(IntPtr hWnd) {
			return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64() : GetWindowLong32(hWnd, GWL_EXSTYLE);
		}

		private static bool IsShellWindow(IntPtr hWnd) {
			var className = new StringBuilder(256);
			GetClassName(hWnd, className, className.Capacity);
			switch(className.ToString()) {
				case "Progman":
				case "WorkerW":
				case "Shell_TrayWnd":
				case "Shell_SecondaryTrayWnd":
					return true;
				default:
					return false;
			}
		}

		private static string GetTitle(IntPtr hWnd) {
			var length = GetWindowTextLength(hWnd);
			if(length == 0) return "";
			var title = new StringBuilder(length + 1);
			GetWindowText(hWnd, title, title.Capacity);
			return title.ToString();
		}

		private static Icon GetSmallIcon(IntPtr hWnd) {
			try {
				IntPtr iconHandle;
				SendMessageTimeout(hWnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero, SMTO_ABORTIFHUNG, 100, out iconHandle);
				if(iconHandle == IntPtr.Zero) iconHandle = IntPtr.Size == 8 ? GetClassLongPtr64(hWnd, GCLP_HICONSM) : (IntPtr)GetClassLong32(hWnd, GCLP_HICONSM);
				return iconHandle == IntPtr.Zero ? null : (Icon)Icon.FromHandle(iconHandle).Clone();
			} catch {
				return null;
			}
		}
	}
}
