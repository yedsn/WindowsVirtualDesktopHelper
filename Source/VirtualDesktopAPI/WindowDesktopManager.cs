using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WindowsVirtualDesktopHelper.VirtualDesktopAPI {
	internal sealed class WindowDesktopLookup : IDisposable {
		private static readonly Guid VirtualDesktopManagerClassId = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");

		[ComImport]
		[Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface IVirtualDesktopManager {
			[return: MarshalAs(UnmanagedType.Bool)]
			bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
			Guid GetWindowDesktopId(IntPtr topLevelWindow);
			void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
		}

		private IVirtualDesktopManager _manager;

		internal WindowDesktopLookup() {
			_manager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(VirtualDesktopManagerClassId));
		}

		internal bool TryGetWindowDesktopId(IntPtr windowHandle, out Guid desktopId) {
			desktopId = Guid.Empty;
			try {
				if(_manager == null) return false;
				desktopId = _manager.GetWindowDesktopId(windowHandle);
				return desktopId != Guid.Empty;
			} catch {
				return false;
			}
		}

		public void Dispose() {
			if(_manager != null && Marshal.IsComObject(_manager)) Marshal.ReleaseComObject(_manager);
			_manager = null;
		}
	}

	internal static class VirtualDesktopRegistry {
		private const string VirtualDesktopsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

		internal static Dictionary<Guid, int> GetDesktopIndices() {
			var desktopIndices = new Dictionary<Guid, int>();
			using(var key = Registry.CurrentUser.OpenSubKey(VirtualDesktopsPath)) {
				var desktopIds = key == null ? null : key.GetValue("VirtualDesktopIDs") as byte[];
				if(desktopIds == null) return desktopIndices;
				for(var offset = 0; offset + 16 <= desktopIds.Length; offset += 16) {
					var idBytes = new byte[16];
					Buffer.BlockCopy(desktopIds, offset, idBytes, 0, idBytes.Length);
					desktopIndices[new Guid(idBytes)] = offset / 16;
				}
			}
			return desktopIndices;
		}

		internal static int GetDesktopCount() {
			return GetDesktopIndices().Count;
		}
	}
}
