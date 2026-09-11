using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	public partial class ErrorForm : Form {
		public ErrorForm() {
			InitializeComponent();
			Localizer.Apply(this);
		}

		public void UpdateUIForError(Exception e) {
			this.labelError.Text = e.Message;
			this.textBoxDetails.Text = e.Message;
			if (e.StackTrace != null) this.textBoxDetails.Text += "\r\n" + e.StackTrace.ToString();
			if (e.InnerException != null) this.textBoxDetails.Text += "\r\n\r\n" + e.InnerException.Message;
			if (e.InnerException != null && e.InnerException.StackTrace != null) this.textBoxDetails.Text += "\r\n" + e.InnerException.StackTrace.ToString();
			this.textBoxDetails.Text += "\r\n";
			this.textBoxDetails.Text += "\r\n" + "Windows Build: " + GetWindowsBuildVersion() + "."+ GetWindowsBuildRevision();
			//this.textBoxDetails.Text += "\r\n" + "Windows Release: " + GetWindowsReleaseId();
			//this.textBoxDetails.Text += "\r\n" + "Windows Product: " + GetWindowsProductName();
			this.textBoxDetails.Text += "\r\n" + "Windows Version: " + GetWindowsDisplayVersion();
			this.textBoxDetails.Text += "\r\n" + "Windows Virtual Desktop Helper Version: " + GetAppBuildVersion();
			this.textBoxDetails.Text += "\r\n" + "Virtual Desktop Implementation: " + App.DetectedVDImplementation;
			this.textBoxDetails.Text += "\r\n";
			this.textBoxDetails.Text += "\r\n" + "Log:";
			this.textBoxDetails.Text += "\r\n" + string.Join("\r\n", Util.Logging.GetLogHistory());
		}

		private string GetAppBuildVersion() {
			try {
				return Assembly.GetExecutingAssembly().GetName().Version.ToString();
			} catch (Exception e) {
				return "Error getting app version: " + e.Message;
			}
		}

		private string GetWindowsBuildVersion() {
			try {
				return Util.OS.GetWindowsBuildVersion().ToString();
			} catch(Exception e) {
				return "Error getting build version: " + e.Message;
			}
		}

		private string GetWindowsBuildRevision() {
			try {
				return Util.OS.GetWindowsBuildRevision().ToString();
			} catch(Exception e) {
				return "Error getting build revision: " + e.Message;
			}
		}

		private string GetWindowsProductName() {
			try {
				return Util.OS.GetWindowsProductName();
			} catch (Exception e) {
				return "Error getting build version: " + e.Message;
			}
		}

		private string GetWindowsDisplayVersion() {
			try {
				return Util.OS.GetWindowsDisplayVersion();
			} catch (Exception e) {
				return "Error getting display version: " + e.Message;
			}
		}

		private string GetWindowsReleaseId() {
			try {
				return Util.OS.GetWindowsReleaseId().ToString();
			} catch (Exception e) {
				return "Error getting release id: " + e.Message;
			}
		}

		private void buttonExit_Click(object sender, EventArgs e) {
			Application.Exit();
			System.Environment.Exit(1);
		}

		public void OpenIssueOnGithub() {
			var url = "https://github.com/dankrusi/WindowsVirtualDesktopHelper/issues/new";
			url += $"?title={Uri.EscapeDataString($"WVDH v{GetAppBuildVersion()} / {GetWindowsProductName()} {GetWindowsDisplayVersion()} {GetWindowsBuildVersion()} / Error: {this.labelError.Text}")}";
			url += $"&body={Uri.EscapeDataString("\n\n" + this.textBoxDetails.Text)}";
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}

		private void buttonOpenIssue_Click(object sender, EventArgs e) {

			string message = Localizer.IsChinese ? "提交 GitHub 问题前，请确认：" : "Before filing a GitHub issue, please make sure of the following:";
			message += "\n";
			message += Localizer.IsChinese ? "\n- 正在使用最新版本" : "\n- You are using the latest version";
			message += Localizer.IsChinese ? "\n- 此问题尚未被报告" : "\n- Your issue doesn't already exist";
			message += Localizer.IsChinese ? "\n- 没有使用修改过的 Windows 版本" : "\n- You are not using patched versions of Windows";
			message += "\n";
			message += "\n" + Localizer.L("Continue?");
			string title = Localizer.L("Issue Checklist");
			MessageBoxButtons buttons = MessageBoxButtons.YesNo;
			DialogResult result = MessageBox.Show(message, title, buttons);
			if (result == DialogResult.Yes) {
				OpenIssueOnGithub();
			} else {
				//this.Close();
			}

		}

		private void button1_Click(object sender, EventArgs e) {
			Process.Start(new ProcessStartInfo("https://github.com/dankrusi/WindowsVirtualDesktopHelper/issues") { UseShellExecute = true });
		}
	}
}
