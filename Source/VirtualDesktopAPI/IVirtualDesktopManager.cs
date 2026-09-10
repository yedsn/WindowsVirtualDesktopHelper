// Original Implemation: windows-virtualdesktopindicator by zgdump (https://github.com/zgdump/windows-virtualdesktopindicator)
// Contributors: Dan Krusi (https://github.com/dankrusi), MScholtes (https://github.com/MScholtes), Flaflo (https://github.com/Flaflo)
// License: MIT License (https://github.com/zgdump/windows-virtualdesktopindicator/blob/main/LICENSE)

using System;

namespace WindowsVirtualDesktopHelper.VirtualDesktopAPI {
	internal interface IWindowDesktopMover {
		void MoveWindowToDesktop(IntPtr windowHandle, int desktopIndex);
	}

	public interface IVirtualDesktopManager {
		uint Current();

		int DisplayCount();

		void SwitchForward();

		void SwitchBackward();

		void SwitchToDesktop(int number);

		string CurrentDisplayName();

		uint GetVDCount();

		void CreateDesktop();

		// Returns the zero-based desktop position for an ownership ID, or -1 when it
		// is not part of the current desktop collection.
		int GetDesktopIndex(Guid desktopId);

		// Re-establishes the connection to the underlying COM API, needed after explorer.exe restarts
		void Reconnect();
	}
}
