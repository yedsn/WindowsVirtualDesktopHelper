using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using WindowsVirtualDesktopHelper.WindowsHotKeyAPI;

namespace WindowsVirtualDesktopHelper {
	public partial class AppForm : Form {

		private bool _startupDone = false;

		public AppForm() {
			// Init UI
			InitializeComponent();
		}



		#region Form Events

		protected override CreateParams CreateParams {
			get {
				CreateParams createParams = base.CreateParams;

				int WS_EX_NOACTIVATE = 0x08000000;
				int WS_EX_LAYERED = 0x80000;
				int WS_EX_TRANSPARENT = 0x20;
				createParams.ExStyle |= WS_EX_NOACTIVATE;
				createParams.ExStyle |= WS_EX_LAYERED;
				createParams.ExStyle |= WS_EX_TRANSPARENT;

				return createParams;
			}
		}

		// This form is only a host for the tray icons and the UI message pump; it must
		// NEVER be visible. The previous approach (WindowState=Minimized + layered styles)
		// did not actually prevent display: Windows could restore and repaint it on events
		// like session unlock, DPI/display changes, or an Explorer/taskbar restart, leaving a
		// stray "Windows Virtual Desktop Manager" dialog with no close button (ControlBox=false).
		// Overriding SetVisibleCore guarantees the window can never be shown, while still
		// creating its handle so Invoke(), the message pump, and the NotifyIcons keep working.
		protected override void SetVisibleCore(bool value) {
			if (!this.IsHandleCreated) this.CreateHandle(); // ensure handle for Invoke/pump/tray
			base.SetVisibleCore(false);                     // but never actually show it
		}

		// Because the form is never shown, the Load event no longer fires, so the startup
		// wiring that used to live in AppForm_Load now runs here, once the handle exists.
		protected override void OnHandleCreated(EventArgs e) {
			base.OnHandleCreated(e);
			if (_startupDone) return;
			_startupDone = true;
			StartUp();
		}

		private void StartUp() {
			App.Instance.ShowSplash();
			App.Instance.MonitorVDSwitch();
			App.Instance.MonitorSystemThemeSwitch();
			App.Instance.MonitorVDisplayCount();
			App.Instance.MonitorFGWindowName();
			App.Instance.MonitorFocusedWindow();

			// Update permanent overlay
			App.Instance.UpdateStatusOverlayWindows();

			App.Instance.UIUpdate();
		}

		private void AppForm_Load(object sender, EventArgs e) {
			// Intentionally empty: startup now runs from OnHandleCreated (see SetVisibleCore).
		}

		private void AppForm_Shown(object sender, EventArgs e) {
			
		}

		private void AppForm_FormClosed(object sender, FormClosedEventArgs e) {

		}

		private void AppForm_FormClosing(object sender, FormClosingEventArgs e) {
			if(e.CloseReason == CloseReason.UserClosing) {
				e.Cancel = true;
				Hide();
			} else if(e.CloseReason == CloseReason.ApplicationExitCall || e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing) {
				// Remove all notif icons
				notifyIconName.Visible = false;
				notifyIconNumber.Visible = false;
				notifyIconPrev.Visible = false;
				notifyIconNext.Visible = false;
			}
		}

		#endregion

		#region Menu and Icon Tray Events

		private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
			if(e.ClickedItem.Tag == null) return;
			if(e.ClickedItem.Tag.ToString().StartsWith("desktop:")) App.Instance.SwitchToDesktop(int.Parse(e.ClickedItem.Tag.ToString().Replace("desktop:", "")));
			else if(e.ClickedItem.Tag.ToString() == "exit") App.Instance.Exit();
			else if(e.ClickedItem.Tag.ToString() == "settings") App.Instance.ShowSettings();
			else if(e.ClickedItem.Tag.ToString() == "about") App.Instance.ShowAbout();
			else if(e.ClickedItem.Tag.ToString() == "donate") App.Instance.OpenDonatePage();
		}

		private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e) {
			UpdateDesktopMenuItems();
		}

		private void UpdateDesktopMenuItems() {
			for(var i = this.contextMenuStrip1.Items.Count - 1; i >= 0; i--) {
				var item = this.contextMenuStrip1.Items[i];
				if(item.Tag != null && item.Tag.ToString().StartsWith("desktop:")) {
					this.contextMenuStrip1.Items.RemoveAt(i);
				}
			}

			var count = Math.Max(1, App.Instance.CurrentVDDisplayCount);
			var current = (int)App.Instance.CurrentVDDisplayNumber;
			var insertIndex = this.contextMenuStrip1.Items.IndexOf(this.toolStripSeparatorDesktops);
			if(insertIndex < 0) insertIndex = 0;

			for(var i = 0; i < count; i++) {
				var item = new ToolStripMenuItem("Desktop " + (i + 1)) { Tag = "desktop:" + i, Checked = i == current };
				this.contextMenuStrip1.Items.Insert(insertIndex + i, item);
			}
		}

		private void notifyIconPrev_Click(object sender, EventArgs e) {
			App.Instance.SwitchDesktopBackward();
		}

		private void notifyIconNext_Click(object sender, EventArgs e) {
			App.Instance.SwitchDesktopForward();
		}

		private void notifyIconPrev_DoubleClick(object sender, EventArgs e) {
			//TODO: got to first desktop
		}

		private void notifyIconNext_DoubleClick(object sender, EventArgs e) {
			//TODO: go to last desktop
		}

		private void notifyIconName_MouseClick(object sender, MouseEventArgs e) {
			if(Settings.GetBool("feature.showDesktopNumberInIconTray.clickToOpenTaskView")) {
				if(e.Button == MouseButtons.Left) {
					App.Instance.OpenTaskView();
				}
			}
		}

		private void notifyIconNumber_MouseClick(object sender, MouseEventArgs e) {
			if (Settings.GetBool("feature.showDesktopNumberInIconTray.clickToOpenTaskView")) {
				if(e.Button == MouseButtons.Left) {
					App.Instance.OpenTaskView();
				}
			}
		}

		#endregion




	}
}
