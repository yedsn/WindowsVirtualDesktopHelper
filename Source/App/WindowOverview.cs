using System;

namespace WindowsVirtualDesktopHelper {
	public sealed class WindowOverviewItem {
		public Util.ApplicationWindow Window { get; private set; }
		public int DesktopIndex { get; private set; }

		public WindowOverviewItem(Util.ApplicationWindow window, int desktopIndex) {
			Window = window;
			DesktopIndex = desktopIndex;
		}

		public string DisplayName {
			get {
				return string.IsNullOrEmpty(Window.Title) ? Window.ProcessName : Window.ProcessName + " - " + Window.Title;
			}
		}
	}
}
