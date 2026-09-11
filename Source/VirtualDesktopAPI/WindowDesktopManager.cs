using System;
using System.Collections.Generic;
using System.Linq;
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

		internal bool TryMoveWindowToDesktop(IntPtr windowHandle, Guid desktopId, out string error) {
			error = null;
			try {
				if(_manager == null || desktopId == Guid.Empty) return false;
				_manager.MoveWindowToDesktop(windowHandle, ref desktopId);
				return true;
			} catch(Exception e) {
				error = e.GetType().Name + ": " + e.Message;
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

		internal static List<string> GetDesktopNames() {
			var desktopNames = new List<string>();
			foreach(var pair in GetDesktopIndices().OrderBy(pair => pair.Value)) {
				string desktopName = null;
				using(var desktopKey = Registry.CurrentUser.OpenSubKey(VirtualDesktopsPath + @"\Desktops\{" + pair.Key + "}")) {
					desktopName = desktopKey == null ? null : desktopKey.GetValue("Name") as string;
				}
				desktopNames.Add(string.IsNullOrEmpty(desktopName) ? "Desktop " + (pair.Value + 1) : desktopName);
			}
			return desktopNames;
		}

		internal static bool TryGetDesktopId(int desktopIndex, out Guid desktopId) {
			desktopId = Guid.Empty;
			foreach(var pair in GetDesktopIndices()) {
				if(pair.Value != desktopIndex) continue;
				desktopId = pair.Key;
				return true;
			}
			return false;
		}

		internal static void SetDesktopName(int desktopIndex, string name) {
			Guid desktopId;
			if(!TryGetDesktopId(desktopIndex, out desktopId)) throw new InvalidOperationException("The virtual desktop is no longer available.");
			using(var key = Registry.CurrentUser.CreateSubKey(VirtualDesktopsPath + @"\Desktops\{" + desktopId + "}")) {
				if(key == null) throw new InvalidOperationException("Windows did not allow the desktop name to be updated.");
				key.SetValue("Name", name ?? string.Empty, RegistryValueKind.String);
			}
		}
	}
}
