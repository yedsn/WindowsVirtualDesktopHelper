using System;

namespace WindowsVirtualDesktopHelper {
	public sealed class WindowOverviewItem {
		public Util.ApplicationWindow Window { get; private set; }
		public int DesktopIndex { get; private set; }
		public bool IsShownOnAllDesktops { get; private set; }
		public bool IsCleanupProtected { get; private set; }

		public WindowOverviewItem(Util.ApplicationWindow window, int desktopIndex, bool isShownOnAllDesktops = false, bool isCleanupProtected = false) {
			Window = window;
			DesktopIndex = desktopIndex;
			IsShownOnAllDesktops = isShownOnAllDesktops;
			IsCleanupProtected = isCleanupProtected;
		}

		public string DisplayName {
			get {
				return string.IsNullOrEmpty(Window.Title) ? Window.ProcessName : Window.Title + " - " + Window.ProcessName;
			}
		}

		public void SetCleanupProtection(bool isCleanupProtected) {
			IsCleanupProtected = isCleanupProtected;
		}
	}
}
