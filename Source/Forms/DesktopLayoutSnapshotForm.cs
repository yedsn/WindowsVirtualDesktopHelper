using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	public sealed class DesktopLayoutSnapshotForm : Form {
		private readonly ListView _snapshots;
		private readonly Button _newButton;
		private readonly Button _restoreButton;
		private readonly Button _updateButton;
		private readonly Button _inspectButton;
		private readonly Button _editWindowRulesButton;
		private readonly Button _copyButton;
		private readonly Button _renameButton;
		private readonly Button _deleteButton;
		private readonly Label _status;
		private List<DesktopLayoutSnapshot> _items = new List<DesktopLayoutSnapshot>();

		public DesktopLayoutSnapshotForm() {
			Text = Localizer.L("Desktop Layout Snapshots");
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(780, 460);
			Size = new Size(980, 620);
			ShowInTaskbar = true;

			var header = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(14, 15, 14, 0), Text = Localizer.L("Desktop Layout Snapshots"), Font = new Font(Font.FontFamily, 13, FontStyle.Bold) };
			_snapshots = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
			_snapshots.Columns.Add(Localizer.L("Name"), 260);
			_snapshots.Columns.Add(Localizer.L("Updated"), 170);
			_snapshots.Columns.Add(Localizer.L("Created"), 170);
			_snapshots.Columns.Add(Localizer.L("Desktops"), 100);
			_snapshots.Columns.Add(Localizer.L("Windows"), 100);

			var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10, 8, 10, 8), FlowDirection = FlowDirection.LeftToRight };
			_newButton = AddButton(actions, Localizer.L("New Snapshot"), (sender, e) => CreateSnapshot());
			_restoreButton = AddButton(actions, Localizer.L("Restore"), (sender, e) => RestoreSelected());
			_updateButton = AddButton(actions, Localizer.L("Update"), (sender, e) => UpdateSelected());
			_copyButton = AddButton(actions, Localizer.L("Copy"), (sender, e) => CopySelected());
			_inspectButton = AddButton(actions, Localizer.L("View Details"), (sender, e) => InspectSelected());
			_editWindowRulesButton = AddButton(actions, Localizer.L("Edit Window Rules"), (sender, e) => EditWindowRules());
			_renameButton = AddButton(actions, Localizer.L("Rename"), (sender, e) => RenameSelected());
			_deleteButton = AddButton(actions, Localizer.L("Delete"), (sender, e) => DeleteSelected());
			_status = new Label { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(12, 4, 12, 0), TextAlign = ContentAlignment.MiddleLeft };

			Controls.Add(_snapshots);
			Controls.Add(_status);
			Controls.Add(actions);
			Controls.Add(header);
			_snapshots.SelectedIndexChanged += (sender, e) => UpdateActionState();
			_snapshots.DoubleClick += (sender, e) => RestoreSelected();
			FormClosing += (sender, e) => { if(e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
			UpdateActionState();
		}

		public void ApplyLocalizedText() {
			Localizer.Apply(this);
			foreach(ColumnHeader column in _snapshots.Columns) column.Text = Localizer.L(column.Text);
		}

		public void RefreshSnapshots() {
			try {
				_items = App.Instance.DesktopLayoutSnapshots.List();
				_snapshots.BeginUpdate();
				_snapshots.Items.Clear();
				foreach(var snapshot in _items) _snapshots.Items.Add(new ListViewItem(new[] { snapshot.Name, snapshot.UpdatedAtUtc.ToLocalTime().ToString("g"), snapshot.CreatedAtUtc.ToLocalTime().ToString("g"), snapshot.DesktopCount.ToString(), snapshot.WindowCount.ToString() }) { Tag = snapshot });
				_snapshots.EndUpdate();
				_status.Text = _items.Count == 0 ? Localizer.L("No desktop layout snapshots saved.") : (Localizer.IsChinese ? "已保存 " + _items.Count + " 个快照。" : _items.Count + " snapshot" + (_items.Count == 1 ? "" : "s") + " saved.");
			} catch(Exception e) { _status.Text = e.Message; }
			UpdateActionState();
		}

		public void RestoreMostRecent() {
			if(_items.Count == 0) {
				_status.Text = Localizer.L("No desktop layout snapshots are available to restore.");
				return;
			}
			foreach(ListViewItem item in _snapshots.Items) item.Selected = false;
			_snapshots.Items[0].Selected = true;
			RestoreSelected();
		}

		public void CreateNewSnapshot() {
			CreateSnapshot();
		}

		private static Button AddButton(Control parent, string text, EventHandler click) {
			var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 0, 6, 0) };
			button.Click += click;
			parent.Controls.Add(button);
			return button;
		}

		private DesktopLayoutSnapshot SelectedSnapshot { get { return _snapshots.SelectedItems.Count == 0 ? null : _snapshots.SelectedItems[0].Tag as DesktopLayoutSnapshot; } }

		private void UpdateActionState() {
			var selected = SelectedSnapshot != null;
			_restoreButton.Enabled = selected;
			_updateButton.Enabled = selected;
			_copyButton.Enabled = selected;
			_inspectButton.Enabled = selected;
			_editWindowRulesButton.Enabled = selected;
			_renameButton.Enabled = selected;
			_deleteButton.Enabled = selected;
		}

		private void CreateSnapshot() {
			DesktopLayoutSnapshot snapshot;
			try { snapshot = App.Instance.DesktopLayoutSnapshots.Capture(DefaultName()); }
			catch(Exception e) { ShowError(Localizer.L("Could not capture the current layout."), e); return; }
			if(!ConfirmCapture(snapshot, true)) return;
			try { App.Instance.DesktopLayoutSnapshots.Create(snapshot); RefreshSnapshots(); _status.Text = Localizer.IsChinese ? "已保存 \"" + snapshot.Name + "\"，包含 " + snapshot.WindowCount + " 个窗口。" : "Saved \"" + snapshot.Name + "\" with " + snapshot.WindowCount + " windows."; }
			catch(Exception e) { ShowError(Localizer.L("Could not save the snapshot."), e); }
		}

		private void UpdateSelected() {
			var selected = SelectedSnapshot;
			if(selected == null) return;
			DesktopLayoutSnapshot snapshot;
			try { snapshot = App.Instance.DesktopLayoutSnapshots.Capture(selected.Name); snapshot.Id = selected.Id; snapshot.CreatedAtUtc = selected.CreatedAtUtc; }
			catch(Exception e) { ShowError(Localizer.L("Could not capture the current layout."), e); return; }
			if(!ConfirmCapture(snapshot, false)) return;
			try { App.Instance.DesktopLayoutSnapshots.Update(snapshot); RefreshSnapshots(); _status.Text = Localizer.IsChinese ? "已更新 \"" + snapshot.Name + "\"，未移动任何窗口。" : "Updated \"" + snapshot.Name + "\" without moving any windows."; }
			catch(Exception e) { ShowError(Localizer.L("Could not update the snapshot."), e); }
		}

		private bool ConfirmCapture(DesktopLayoutSnapshot snapshot, bool isNew) {
			using(var dialog = new Form { Text = Localizer.L(isNew ? "New Desktop Layout Snapshot" : "Update Desktop Layout Snapshot"), StartPosition = FormStartPosition.CenterParent, Size = new Size(640, 480), MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog }) {
				var name = new TextBox { Dock = DockStyle.Top, Text = snapshot.Name, Margin = new Padding(12), Height = 25 };
				var label = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(12, 10, 12, 0), Text = snapshot.DesktopCount + " virtual desktop" + (snapshot.DesktopCount == 1 ? "" : "s") + " and " + snapshot.WindowCount + " eligible window" + (snapshot.WindowCount == 1 ? "" : "s") + " detected.\r\nOnly desktop assignment and application identity are saved." };
				var preview = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = DescribeSavedWindows(snapshot), BorderStyle = BorderStyle.FixedSingle };
				var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft };
				var save = AddButton(buttons, Localizer.L(isNew ? "Save Snapshot" : "Update Snapshot"), null); save.DialogResult = DialogResult.OK;
				var cancel = AddButton(buttons, Localizer.L("Cancel"), null); cancel.DialogResult = DialogResult.Cancel;
				dialog.Controls.Add(preview); dialog.Controls.Add(label); dialog.Controls.Add(name); dialog.Controls.Add(buttons); dialog.AcceptButton = save; dialog.CancelButton = cancel;
				if(dialog.ShowDialog(this) != DialogResult.OK) return false;
				if(string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show(this, Localizer.L("A snapshot name is required."), Localizer.L("Desktop Layout Snapshots"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
				snapshot.Name = name.Text.Trim();
				return true;
			}
		}

		private void RestoreSelected() {
			var selected = SelectedSnapshot;
			if(selected == null) return;
			SnapshotRestorePreview preview;
			try { preview = App.Instance.DesktopLayoutSnapshots.Analyze(selected); }
			catch(Exception e) { ShowError(Localizer.L("Could not analyze the snapshot."), e); return; }
			var message = "Only currently open windows with a unique reliable match will be moved.\r\n\r\nCan restore: " + preview.Count(SnapshotRestoreStatus.CanRestore) + "\r\nAlready on target desktop: " + preview.Count(SnapshotRestoreStatus.AlreadyCorrect) + "\r\nNot found: " + preview.Count(SnapshotRestoreStatus.NotFound) + "\r\nAmbiguous match: " + preview.Count(SnapshotRestoreStatus.Ambiguous) + "\r\n\r\nNo application will be started or closed.";
			using(var details = new RestorePreviewForm(selected.Name, message, preview.Items)) {
				if(details.ShowDialog(this) != DialogResult.OK) return;
			}
			ExecuteRestore(preview);
		}

		private void ExecuteRestore(SnapshotRestorePreview preview) {
			using(var progress = new RestoreProgressForm(preview.Snapshot.Name)) {
				var worker = new Thread(() => {
					try {
						App.Instance.DesktopLayoutSnapshots.Restore(preview, (current, total, item) => progress.SetProgress(current, total, item.SavedWindow.DisplayName));
						progress.Complete(preview.Items);
					} catch(Exception e) { progress.Fail(e); }
					}) { IsBackground = true };
				worker.SetApartmentState(ApartmentState.STA);
				worker.Start();
				progress.ShowDialog(this);
			}
			RefreshSnapshots();
			Hide();
			var windowManager = App.Instance.WindowOverviewForm;
			if(windowManager != null && !windowManager.IsDisposed && windowManager.Visible) windowManager.RefreshSnapshot();
		}

		private void InspectSelected() {
			var selected = SelectedSnapshot;
			if(selected == null) return;
			using(var details = new SnapshotDetailsForm(selected.Name, DescribeSavedWindows(selected))) details.ShowDialog(this);
		}

		private void CopySelected() {
			var selected = SelectedSnapshot;
			if(selected == null) return;
			using(var dialog = new Form { Text = Localizer.L("Copy Snapshot"), StartPosition = FormStartPosition.CenterParent, Size = new Size(430, 145), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false }) {
				var name = new TextBox { Dock = DockStyle.Top, Text = "Copy of " + selected.Name, Margin = new Padding(12) };
				var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft };
				var copy = AddButton(buttons, Localizer.L("Copy"), null); copy.DialogResult = DialogResult.OK;
				var cancel = AddButton(buttons, Localizer.L("Cancel"), null); cancel.DialogResult = DialogResult.Cancel;
				dialog.Controls.Add(name); dialog.Controls.Add(buttons); dialog.AcceptButton = copy; dialog.CancelButton = cancel;
				if(dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(name.Text)) return;
				try { App.Instance.DesktopLayoutSnapshots.Copy(selected, name.Text); RefreshSnapshots(); _status.Text = "Copied \"" + selected.Name + "\"."; }
				catch(Exception e) { ShowError(Localizer.L("Could not copy the snapshot."), e); }
			}
		}

		private void EditWindowRules() {
			var selected = SelectedSnapshot;
			if(selected == null) return;
			using(var dialog = new WindowRulesForm(selected)) {
				if(dialog.ShowDialog(this) != DialogResult.OK) return;
			}
			try { App.Instance.DesktopLayoutSnapshots.Update(selected); RefreshSnapshots(); _status.Text = "Updated window matching rules for \"" + selected.Name + "\"."; }
			catch(Exception e) { ShowError(Localizer.L("Could not save window matching rules."), e); }
		}

		private void RenameSelected() {
			var selected = SelectedSnapshot;
			if(selected == null) return;
			using(var dialog = new Form { Text = Localizer.L("Rename Snapshot"), StartPosition = FormStartPosition.CenterParent, Size = new Size(430, 145), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false }) {
				var name = new TextBox { Dock = DockStyle.Top, Text = selected.Name, Margin = new Padding(12) };
				var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft };
				var save = AddButton(buttons, Localizer.L("Rename"), null); save.DialogResult = DialogResult.OK;
				var cancel = AddButton(buttons, Localizer.L("Cancel"), null); cancel.DialogResult = DialogResult.Cancel;
				dialog.Controls.Add(name); dialog.Controls.Add(buttons); dialog.AcceptButton = save; dialog.CancelButton = cancel;
				if(dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(name.Text)) return;
				try { App.Instance.DesktopLayoutSnapshots.Rename(selected.Id, name.Text); RefreshSnapshots(); }
				catch(Exception e) { ShowError(Localizer.L("Could not rename the snapshot."), e); }
			}
		}

		private void DeleteSelected() {
			var selected = SelectedSnapshot;
			if(selected == null || MessageBox.Show(this, (Localizer.IsChinese ? "删除 \"" + selected.Name + "\"？这不会影响已打开的应用程序或桌面。" : "Delete \"" + selected.Name + "\"? This does not affect open applications or desktops."), Localizer.L("Delete Snapshot"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
			try { App.Instance.DesktopLayoutSnapshots.Delete(selected.Id); RefreshSnapshots(); }
			catch(Exception e) { ShowError(Localizer.L("Could not delete the snapshot."), e); }
		}

		private static string DefaultName() { return "Snapshot " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"); }
		internal static string DescribeSavedWindows(DesktopLayoutSnapshot snapshot) { return string.Join(Environment.NewLine, snapshot.Desktops.OrderBy(desktop => desktop.Index).Select(desktop => "Desktop " + (desktop.Index + 1) + (string.IsNullOrEmpty(desktop.Name) ? "" : " - " + desktop.Name) + Environment.NewLine + string.Join(Environment.NewLine, snapshot.Windows.Where(window => window.DesktopIndex == desktop.Index).Select(window => "  " + window.DisplayName + (window.WindowTitleIsRegex ? " [regular expression]" : ""))))); }
		private void ShowError(string message, Exception error) { _status.Text = message; MessageBox.Show(this, message + "\r\n\r\n" + error.Message, "Desktop Layout Snapshots", MessageBoxButtons.OK, MessageBoxIcon.Error); }
	}

	internal sealed class WindowRulesForm : Form {
		private readonly DesktopLayoutSnapshot _snapshot;
		private readonly ListView _windows;
		private readonly NumericUpDown _desktopNumber;
		private readonly TextBox _windowTitle;
		private readonly TextBox _applicationName;
		private readonly CheckBox _useRegularExpression;
		private readonly Dictionary<SnapshotWindow, WindowRuleState> _originalRules;
		private readonly List<SnapshotWindow> _originalWindows;

		private sealed class WindowRuleState {
			public int DesktopIndex;
			public string DesktopId;
			public string WindowTitle;
			public bool WindowTitleIsRegex;
			public string ProcessName;
			public bool ApplicationNameIsOverride;
		}

		internal WindowRulesForm(DesktopLayoutSnapshot snapshot) {
			_snapshot = snapshot;
			Text = Localizer.L("Window Matching Rules") + " - " + snapshot.Name;
			StartPosition = FormStartPosition.CenterParent;
			MinimumSize = new Size(780, 470);
			Size = new Size(940, 620);
			_windows = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
			_windows.Columns.Add(Localizer.L("Target desktop"), 100);
			_windows.Columns.Add(Localizer.L("Window"), 450);
			_windows.Columns.Add(Localizer.L("Application"), 280);
			_originalWindows = snapshot.Windows.ToList();
			_originalRules = _originalWindows.ToDictionary(window => window, window => new WindowRuleState { DesktopIndex = window.DesktopIndex, DesktopId = window.DesktopId, WindowTitle = window.WindowTitle, WindowTitleIsRegex = window.WindowTitleIsRegex, ProcessName = window.ProcessName, ApplicationNameIsOverride = window.ApplicationNameIsOverride });
			foreach(var window in snapshot.Windows.OrderBy(item => item.DesktopIndex).ThenBy(item => item.DisplayName)) _windows.Items.Add(CreateItem(window));
			var help = new Label { Dock = DockStyle.Top, Height = 92, Padding = new Padding(12, 8, 12, 0), Text = "Edit the selected rule fields below. Target desktop is where the matched window will be moved.\r\nEnable regular expressions when only part of a changing window title should match. Examples: Project Alpha; ^Project Alpha.*; ^Project Alpha.* - Visual Studio$.\r\nChanging Application uses the entered process name as the application match. Matching ignores case; multiple matching windows are shown as ambiguous and are not moved." };
			var editor = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 106, Padding = new Padding(12, 8, 12, 8), ColumnCount = 4, RowCount = 2 };
			editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			editor.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); editor.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			_desktopNumber = new NumericUpDown { Minimum = 1, Maximum = 999, Anchor = AnchorStyles.Left | AnchorStyles.Right };
			_windowTitle = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
			_applicationName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
			_useRegularExpression = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, Text = Localizer.L("Use regular expression"), TextAlign = ContentAlignment.MiddleLeft };
			var apply = new Button { Anchor = AnchorStyles.Right, Width = 72, Text = Localizer.L("Apply") };
			var delete = new Button { Anchor = AnchorStyles.Right, Width = 72, Text = Localizer.L("Delete") };
			var ruleActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
			ruleActions.Controls.Add(apply); ruleActions.Controls.Add(delete);
			editor.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Localizer.L("Target desktop"), TextAlign = ContentAlignment.MiddleLeft }, 0, 0); editor.Controls.Add(_desktopNumber, 1, 0);
			editor.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Localizer.L("Application"), TextAlign = ContentAlignment.MiddleLeft }, 2, 0); editor.Controls.Add(_applicationName, 3, 0);
			editor.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Localizer.L("Window"), TextAlign = ContentAlignment.MiddleLeft }, 0, 1); editor.Controls.Add(_windowTitle, 1, 1);
			editor.Controls.Add(_useRegularExpression, 2, 1); editor.Controls.Add(ruleActions, 3, 1);
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft };
			var save = new Button { Text = Localizer.L("Save"), AutoSize = true };
			var cancel = new Button { Text = Localizer.L("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
			buttons.Controls.Add(save); buttons.Controls.Add(cancel);
			Controls.Add(_windows); Controls.Add(editor); Controls.Add(buttons); Controls.Add(help);
			_windows.SelectedIndexChanged += (sender, e) => ShowSelectedRule();
			apply.Click += (sender, e) => ApplyRule();
			delete.Click += (sender, e) => DeleteSelectedRule();
			save.Click += (sender, e) => { if(ApplyRule()) DialogResult = DialogResult.OK; };
			FormClosing += (sender, e) => { if(DialogResult != DialogResult.OK) { _snapshot.Windows.Clear(); _snapshot.Windows.AddRange(_originalWindows); foreach(var rule in _originalRules) { rule.Key.DesktopIndex = rule.Value.DesktopIndex; rule.Key.DesktopId = rule.Value.DesktopId; rule.Key.WindowTitle = rule.Value.WindowTitle; rule.Key.WindowTitleIsRegex = rule.Value.WindowTitleIsRegex; rule.Key.ProcessName = rule.Value.ProcessName; rule.Key.ApplicationNameIsOverride = rule.Value.ApplicationNameIsOverride; } } };
			if(_windows.Items.Count > 0) _windows.Items[0].Selected = true;
			AcceptButton = save;
			CancelButton = cancel;
		}

		private ListViewItem CreateItem(SnapshotWindow window) { return new ListViewItem(new[] { (window.DesktopIndex + 1).ToString(), window.WindowTitle ?? "", window.ProcessName ?? "" }) { Tag = window }; }
		private SnapshotWindow SelectedWindow { get { return _windows.SelectedItems.Count == 0 ? null : _windows.SelectedItems[0].Tag as SnapshotWindow; } }
		private void ShowSelectedRule() { var window = SelectedWindow; _desktopNumber.Value = window == null ? 1 : Math.Min(_desktopNumber.Maximum, window.DesktopIndex + 1); _desktopNumber.Enabled = window != null; _windowTitle.Text = window == null ? "" : window.WindowTitle ?? ""; _windowTitle.Enabled = window != null; _applicationName.Text = window == null ? "" : window.ProcessName ?? ""; _applicationName.Enabled = window != null; _useRegularExpression.Checked = window != null && window.WindowTitleIsRegex; _useRegularExpression.Enabled = window != null; }
		private void DeleteSelectedRule() {
			var window = SelectedWindow;
			if(window == null) return;
			if(MessageBox.Show(this, Localizer.IsChinese ? "从快照中移除此窗口规则？该窗口将不再被恢复。" : "Remove this window rule from the snapshot? The window will no longer be restored by this snapshot.", Localizer.L("Delete Rule"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
			var index = _windows.SelectedIndices[0];
			_snapshot.Windows.Remove(window);
			_windows.Items.RemoveAt(index);
			if(_windows.Items.Count > 0) _windows.Items[Math.Min(index, _windows.Items.Count - 1)].Selected = true;
		}
		private bool ApplyRule() {
			var window = SelectedWindow;
			if(window == null) return true;
			var title = _windowTitle.Text.Trim();
			var application = _applicationName.Text.Trim();
			if(string.IsNullOrEmpty(title)) { MessageBox.Show(this, Localizer.L("A window name is required."), Localizer.L("Window Matching Rules"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
			if(string.IsNullOrEmpty(application)) { MessageBox.Show(this, Localizer.L("An application name is required."), Localizer.L("Window Matching Rules"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
			if(_useRegularExpression.Checked) try { new System.Text.RegularExpressions.Regex(title, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)); }
			catch(ArgumentException e) { MessageBox.Show(this, Localizer.L("The title regular expression is invalid.") + "\r\n\r\n" + e.Message, Localizer.L("Window Matching Rules"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
			window.DesktopIndex = Decimal.ToInt32(_desktopNumber.Value) - 1;
			window.DesktopId = null;
			window.WindowTitle = title;
			window.WindowTitleIsRegex = _useRegularExpression.Checked;
			window.ProcessName = application;
			window.ApplicationNameIsOverride = window.ApplicationNameIsOverride || !string.Equals(application, _originalRules[window].ProcessName, StringComparison.OrdinalIgnoreCase);
			_windows.SelectedItems[0].SubItems[0].Text = (window.DesktopIndex + 1).ToString();
			_windows.SelectedItems[0].SubItems[1].Text = window.WindowTitle;
			_windows.SelectedItems[0].SubItems[2].Text = window.ProcessName;
			return true;
		}
	}

	internal sealed class SnapshotDetailsForm : Form {
		internal SnapshotDetailsForm(string name, string text) {
			Text = name; StartPosition = FormStartPosition.CenterParent; Size = new Size(650, 520);
			Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = text });
		}
	}

	internal sealed class RestorePreviewForm : Form {
		internal RestorePreviewForm(string name, string summary, List<SnapshotRestoreItem> items) {
			Text = "Restore \"" + name + "\""; StartPosition = FormStartPosition.CenterParent; Size = new Size(720, 540); MinimizeBox = false; MaximizeBox = false;
			var info = new Label { Dock = DockStyle.Top, Height = 152, Padding = new Padding(12), Text = summary };
			var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
			list.Columns.Add("Status", 150); list.Columns.Add("Window", 300); list.Columns.Add("Reason", 230);
			foreach(var item in items) list.Items.Add(new ListViewItem(new[] { StatusText(item.Status), item.SavedWindow.DisplayName, item.Reason }));
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft };
			var restore = new Button { Text = "Restore", DialogResult = DialogResult.OK, AutoSize = true }; var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
			buttons.Controls.Add(restore); buttons.Controls.Add(cancel); Controls.Add(list); Controls.Add(info); Controls.Add(buttons); AcceptButton = restore; CancelButton = cancel;
		}
		internal static string StatusText(SnapshotRestoreStatus status) { return status == SnapshotRestoreStatus.CanRestore ? "Can restore" : status == SnapshotRestoreStatus.AlreadyCorrect ? "Already correct" : status == SnapshotRestoreStatus.NotFound ? "Not found" : status == SnapshotRestoreStatus.Ambiguous ? "Ambiguous match" : status.ToString(); }
	}

	internal sealed class RestoreProgressForm : Form {
		private readonly Label _label;
		private readonly ProgressBar _progress;
		private readonly Button _done;
		private readonly Button _details;
		private List<SnapshotRestoreItem> _items;
		internal RestoreProgressForm(string name) {
			Text = "Restoring \"" + name + "\""; StartPosition = FormStartPosition.CenterParent; Size = new Size(560, 180); FormBorderStyle = FormBorderStyle.FixedDialog; ControlBox = false;
			_label = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(12, 15, 12, 0), Text = "Preparing restore..." };
			_progress = new ProgressBar { Dock = DockStyle.Top, Height = 24, Margin = new Padding(12), Style = ProgressBarStyle.Marquee };
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 7, 12, 7), FlowDirection = FlowDirection.RightToLeft };
			_done = new Button { Text = "Done", AutoSize = true, Enabled = false, DialogResult = DialogResult.OK };
			_details = new Button { Text = "View Details", AutoSize = true, Enabled = false };
			_details.Click += (sender, e) => ShowDetails();
			buttons.Controls.Add(_done); buttons.Controls.Add(_details);
			Controls.Add(buttons); Controls.Add(_progress); Controls.Add(_label); AcceptButton = _done;
		}
		internal void SetProgress(int current, int total, string name) { InvokeSafe(() => { _progress.Style = ProgressBarStyle.Continuous; _progress.Maximum = Math.Max(1, total); _progress.Value = Math.Min(current, _progress.Maximum); _label.Text = "Processing " + current + " of " + total + ": " + name; }); }
		internal void Complete(List<SnapshotRestoreItem> items) { InvokeSafe(() => { _items = items; _progress.Style = ProgressBarStyle.Continuous; _progress.Value = _progress.Maximum; _label.Text = "Restore complete: " + items.Count(item => item.Status == SnapshotRestoreStatus.Moved) + " moved, " + items.Count(item => item.Status == SnapshotRestoreStatus.AlreadyCorrect) + " already correct, " + items.Count(item => item.Status == SnapshotRestoreStatus.NotFound) + " not found, " + items.Count(item => item.Status == SnapshotRestoreStatus.Ambiguous) + " ambiguous, " + items.Count(item => item.Status == SnapshotRestoreStatus.Failed) + " failed."; _details.Enabled = true; _done.Enabled = true; }); }
		internal void Fail(Exception error) { InvokeSafe(() => { _progress.Style = ProgressBarStyle.Continuous; _label.Text = "Restore could not complete: " + error.Message; _done.Enabled = true; }); }
		private void ShowDetails() {
			if(_items == null) return;
			var text = string.Join(Environment.NewLine, _items.Select(item => RestorePreviewForm.StatusText(item.Status) + " - " + item.SavedWindow.DisplayName + Environment.NewLine + "  " + item.Reason));
			using(var details = new SnapshotDetailsForm("Restore Details", text)) details.ShowDialog(this);
		}
		private void InvokeSafe(Action action) { if(IsDisposed || !IsHandleCreated) return; try { BeginInvoke(action); } catch(InvalidOperationException) { } }
	}
}
