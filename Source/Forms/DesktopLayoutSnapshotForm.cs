using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	public sealed class DesktopLayoutSnapshotForm : Form {
		private readonly ListView _snapshots;
		private readonly Button _captureButton;
		private readonly Button _restoreButton;
		private readonly Button _inspectButton;
		private readonly Button _renameButton;
		private readonly Button _deleteButton;
		private readonly CheckBox _automaticSnapshots;
		private readonly NumericUpDown _intervalMinutes;
		private readonly NumericUpDown _maximumSnapshots;
		private readonly Label _status;
		private List<RuleSnapshot> _items = new List<RuleSnapshot>();

		public DesktopLayoutSnapshotForm() {
			Text = Localizer.L("Rule Snapshots");
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(820, 500);
			Size = new Size(1020, 650);
			ShowInTaskbar = true;

			var header = new Label { Dock = DockStyle.Top, Height = 52, Padding = new Padding(14, 14, 14, 0), Text = Localizer.L("Rule Snapshots"), Font = new Font(Font.FontFamily, 13, FontStyle.Bold) };
			var schedule = new TableLayoutPanel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 6, 12, 6), ColumnCount = 6 };
			schedule.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			schedule.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			schedule.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			schedule.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			schedule.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			schedule.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			_automaticSnapshots = new CheckBox { Text = Localizer.L("Automatic snapshots"), AutoSize = true, Anchor = AnchorStyles.Left };
			_intervalMinutes = new NumericUpDown { Minimum = 1, Maximum = 1440, Width = 64, Anchor = AnchorStyles.Left };
			_maximumSnapshots = new NumericUpDown { Minimum = 0, Maximum = 99, Width = 64, Anchor = AnchorStyles.Left };
			schedule.Controls.Add(_automaticSnapshots, 0, 0);
			schedule.Controls.Add(_intervalMinutes, 1, 0);
			schedule.Controls.Add(new Label { Text = Localizer.L("minutes"), AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
			schedule.Controls.Add(new Label { Text = Localizer.L("Keep automatic snapshots"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(18, 3, 3, 3) }, 3, 0);
			schedule.Controls.Add(_maximumSnapshots, 4, 0);
			var saveSchedule = new Button { Text = Localizer.L("Save"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 3, 3, 3) };
			schedule.Controls.Add(saveSchedule, 5, 0);

			_snapshots = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
			_snapshots.Columns.Add(Localizer.L("Name"), 260);
			_snapshots.Columns.Add(Localizer.L("Captured"), 180);
			_snapshots.Columns.Add(Localizer.L("Type"), 110);
			_snapshots.Columns.Add(Localizer.L("Lock rules"), 120);
			_snapshots.Columns.Add(Localizer.L("Desktop Rules"), 120);

			var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10, 8, 10, 8), FlowDirection = FlowDirection.LeftToRight };
			_captureButton = AddButton(actions, Localizer.L("Capture Snapshot"), (sender, e) => CaptureSnapshot());
			_restoreButton = AddButton(actions, Localizer.L("Restore Rules"), (sender, e) => RestoreSelected());
			_inspectButton = AddButton(actions, Localizer.L("View Details"), (sender, e) => InspectSelected());
			_renameButton = AddButton(actions, Localizer.L("Rename"), (sender, e) => RenameSelected());
			_deleteButton = AddButton(actions, Localizer.L("Delete"), (sender, e) => DeleteSelected());
			_status = new Label { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(12, 4, 12, 0), TextAlign = ContentAlignment.MiddleLeft };

			Controls.Add(_snapshots); Controls.Add(_status); Controls.Add(actions); Controls.Add(schedule); Controls.Add(header);
			_snapshots.SelectedIndexChanged += (sender, e) => UpdateActionState();
			_snapshots.DoubleClick += (sender, e) => RestoreSelected();
			saveSchedule.Click += (sender, e) => SaveSchedule();
			FormClosing += (sender, e) => { if(e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
			UpdateActionState();
		}

		public void ApplyLocalizedText() { Localizer.Apply(this); foreach(ColumnHeader column in _snapshots.Columns) column.Text = Localizer.L(column.Text); }
		public void RefreshSnapshots() {
			try {
				_items = App.Instance.DesktopRuleState.ListSnapshots();
				_automaticSnapshots.Checked = Settings.GetBool("desktopRules.autoSnapshot.enabled", false);
				_intervalMinutes.Value = Math.Max(_intervalMinutes.Minimum, Math.Min(_intervalMinutes.Maximum, Settings.GetInt("desktopRules.autoSnapshot.intervalMinutes", 30)));
				_maximumSnapshots.Value = Math.Max(_maximumSnapshots.Minimum, Math.Min(_maximumSnapshots.Maximum, Settings.GetInt("desktopRules.autoSnapshot.maximumCount", 3)));
				_snapshots.BeginUpdate(); _snapshots.Items.Clear();
				foreach(var snapshot in _items) _snapshots.Items.Add(new ListViewItem(new[] { snapshot.Name, snapshot.CreatedAtUtc.ToLocalTime().ToString("g"), KindText(snapshot.Kind), snapshot.CleanupRulesAvailable ? snapshot.CleanupRuleCount.ToString() : Localizer.L("Current"), snapshot.DesktopRuleCount.ToString() }) { Tag = snapshot });
				_snapshots.EndUpdate();
				_status.Text = _items.Count == 0 ? Localizer.L("No rule snapshots saved.") : (Localizer.IsChinese ? "已保存 " + _items.Count + " 个规则快照。" : _items.Count + " rule snapshot" + (_items.Count == 1 ? "" : "s") + " saved.");
			} catch(Exception e) { _status.Text = e.Message; }
			UpdateActionState();
		}
		private static Button AddButton(Control parent, string text, EventHandler click) { var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 0, 6, 0) }; button.Click += click; parent.Controls.Add(button); return button; }
		private RuleSnapshot SelectedSnapshot { get { return _snapshots.SelectedItems.Count == 0 ? null : _snapshots.SelectedItems[0].Tag as RuleSnapshot; } }
		private RuleSnapshot CurrentSnapshot(RuleSnapshot selected) { return selected == null ? null : App.Instance.DesktopRuleState.GetSnapshot(selected.Id); }
		private void UpdateActionState() { var selected = SelectedSnapshot != null; _restoreButton.Enabled = selected; _inspectButton.Enabled = selected; _renameButton.Enabled = selected; _deleteButton.Enabled = selected; }
		private void SaveSchedule() {
			Settings.SetBool("desktopRules.autoSnapshot.enabled", _automaticSnapshots.Checked);
			Settings.SetInt("desktopRules.autoSnapshot.intervalMinutes", Decimal.ToInt32(_intervalMinutes.Value));
			Settings.SetInt("desktopRules.autoSnapshot.maximumCount", Decimal.ToInt32(_maximumSnapshots.Value));
			Settings.SaveConfig(); App.Instance.DesktopRuleSnapshotScheduler.Reload();
			_status.Text = Localizer.L("Automatic snapshot settings saved.");
		}
		private void CaptureSnapshot() {
			using(var dialog = new NameDialog(Localizer.L("Capture Rule Snapshot"), "Snapshot " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"))) {
				if(dialog.ShowDialog(this) != DialogResult.OK) return;
				try { var snapshot = App.Instance.DesktopRuleState.CreateSnapshot(dialog.Value, RuleSnapshotKind.Manual); RefreshSnapshots(); _status.Text = Localizer.IsChinese ? "已保存规则快照 \"" + snapshot.Name + "\"。" : "Saved rule snapshot \"" + snapshot.Name + "\"."; }
				catch(Exception e) { ShowError(Localizer.L("Could not save the rule snapshot."), e); }
			}
		}
		private void RestoreSelected() {
			var selected = CurrentSnapshot(SelectedSnapshot); if(selected == null) { RefreshSnapshots(); return; }
			var limitation = selected.CleanupRulesAvailable ? "" : "\r\n\r\n" + Localizer.L("This legacy snapshot does not include lock rules. Current lock rules will be kept.");
			var message = Localizer.L("Restore this snapshot's desktop rules and lock rules? Open windows will not be moved. Use Apply Desktop Rules afterwards to rearrange windows.") + limitation;
			if(MessageBox.Show(this, message, Localizer.L("Restore Rules"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
			try { App.Instance.DesktopRuleState.RestoreSnapshot(selected.Id); App.Instance.WindowCleanupLocks.Reload(); _status.Text = Localizer.L("Rules restored. No windows were moved."); RefreshSnapshots(); var manager = App.Instance.WindowOverviewForm; if(manager != null && manager.Visible) manager.RefreshSnapshot(); }
			catch(Exception e) { ShowError(Localizer.L("Could not restore the rule snapshot."), e); }
		}
		private void InspectSelected() { var selected = CurrentSnapshot(SelectedSnapshot); if(selected == null) { RefreshSnapshots(); return; } using(var details = new SnapshotDetailsForm(selected.Name, DescribeSnapshot(selected))) details.ShowDialog(this); }
		private void RenameSelected() { var selected = CurrentSnapshot(SelectedSnapshot); if(selected == null) { RefreshSnapshots(); return; } using(var dialog = new NameDialog(Localizer.L("Rename Snapshot"), selected.Name)) { if(dialog.ShowDialog(this) != DialogResult.OK) return; try { App.Instance.DesktopRuleState.RenameSnapshot(selected.Id, dialog.Value); RefreshSnapshots(); } catch(Exception e) { ShowError(Localizer.L("Could not rename the snapshot."), e); } } }
		private void DeleteSelected() { var selected = CurrentSnapshot(SelectedSnapshot); if(selected == null) { RefreshSnapshots(); return; } if(MessageBox.Show(this, Localizer.L("Delete this rule snapshot? Current rules and open windows are not changed."), Localizer.L("Delete Snapshot"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return; try { App.Instance.DesktopRuleState.DeleteSnapshot(selected.Id); RefreshSnapshots(); } catch(Exception e) { ShowError(Localizer.L("Could not delete the snapshot."), e); } }
		private static string KindText(RuleSnapshotKind kind) { return Localizer.L(kind == RuleSnapshotKind.Manual ? "Manual" : kind == RuleSnapshotKind.Automatic ? "Automatic" : "Legacy"); }
		internal static string DescribeSnapshot(RuleSnapshot snapshot) {
			var lines = new List<string> { Localizer.L("Lock rules") + ": " + (snapshot.CleanupRulesAvailable ? snapshot.CleanupRuleCount.ToString() : Localizer.L("Current rules retained when restored")) };
			if(snapshot.CleanupRulesAvailable) {
				foreach(var rule in snapshot.CleanupRules.OrderBy(rule => rule.ProcessName).ThenBy(rule => rule.WindowTitle)) lines.Add("  " + (rule.ProcessName ?? "") + ": " + (rule.WindowTitle ?? "") + (rule.WindowTitleIsRegex ? Localizer.L(" [regular expression]") : ""));
			}
			lines.Add("");
			lines.Add(Localizer.L("Desktop Rules") + ": " + snapshot.DesktopRuleCount);
			lines.AddRange(snapshot.DesktopRules.OrderBy(rule => rule.DesktopIndex).Select(rule => Localizer.L("Desktop ") + (rule.DesktopIndex + 1) + ": " + rule.DisplayName + (rule.WindowTitleIsRegex ? Localizer.L(" [regular expression]") : "")));
			return string.Join(Environment.NewLine, lines);
		}
		private void ShowError(string message, Exception error) { _status.Text = message; MessageBox.Show(this, message + "\r\n\r\n" + error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
	}

	internal sealed class DesktopRulesForm : Form {
		private readonly ListView _rulesList;
		private readonly NumericUpDown _desktopNumber;
		private readonly TextBox _windowTitle;
		private readonly TextBox _applicationName;
		private readonly CheckBox _useRegularExpression;
		private readonly List<DesktopRule> _rules;
		private readonly Button _applyButton;
		private readonly Button _deleteButton;
		internal DesktopRulesForm() {
			Text = Localizer.L("Desktop Rules"); StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(820, 500); Size = new Size(980, 640); ShowInTaskbar = false;
			_rules = App.Instance.DesktopRules.List();
			var help = new Label { Dock = DockStyle.Top, Height = 66, Padding = new Padding(12, 8, 12, 0), Text = Localizer.L("Desktop rules apply across all virtual desktops. Edit the target desktop and matching logic. Regular expressions are useful when window titles change.") };
			_rulesList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
			_rulesList.Columns.Add(Localizer.L("Target desktop"), 110); _rulesList.Columns.Add(Localizer.L("Window"), 440); _rulesList.Columns.Add(Localizer.L("Application"), 250); _rulesList.Columns.Add(Localizer.L("Match type"), 120);
			var editor = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 106, Padding = new Padding(12, 8, 12, 8), ColumnCount = 4, RowCount = 2 };
			editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			_desktopNumber = new NumericUpDown { Minimum = 1, Maximum = 999, Anchor = AnchorStyles.Left | AnchorStyles.Right }; _windowTitle = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right }; _applicationName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right }; _useRegularExpression = new CheckBox { Text = Localizer.L("Use regular expression"), AutoSize = true, Anchor = AnchorStyles.Left };
			_applyButton = new Button { Text = Localizer.L("Apply"), Width = 72, Anchor = AnchorStyles.Right }; _deleteButton = new Button { Text = Localizer.L("Delete"), Width = 72, Anchor = AnchorStyles.Right };
			var editActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false }; editActions.Controls.Add(_applyButton); editActions.Controls.Add(_deleteButton);
			editor.Controls.Add(new Label { Text = Localizer.L("Target desktop"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); editor.Controls.Add(_desktopNumber, 1, 0); editor.Controls.Add(new Label { Text = Localizer.L("Application"), AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0); editor.Controls.Add(_applicationName, 3, 0); editor.Controls.Add(new Label { Text = Localizer.L("Window"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); editor.Controls.Add(_windowTitle, 1, 1); editor.Controls.Add(_useRegularExpression, 2, 1); editor.Controls.Add(editActions, 3, 1);
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft }; var save = new Button { Text = Localizer.L("Save"), AutoSize = true }; var cancel = new Button { Text = Localizer.L("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel }; var add = new Button { Text = Localizer.L("New Desktop Rule"), AutoSize = true }; buttons.Controls.Add(save); buttons.Controls.Add(cancel); buttons.Controls.Add(add);
			Controls.Add(_rulesList); Controls.Add(editor); Controls.Add(buttons); Controls.Add(help);
			_rulesList.SelectedIndexChanged += (sender, e) => ShowSelected(); _applyButton.Click += (sender, e) => ApplySelected(); _deleteButton.Click += (sender, e) => DeleteSelected(); add.Click += (sender, e) => AddRule(); save.Click += (sender, e) => { if(ApplySelected()) { try { App.Instance.DesktopRules.ReplaceAll(_rules); DialogResult = DialogResult.OK; } catch(Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); } } };
			RefreshRules(); AcceptButton = save; CancelButton = cancel;
		}
		private DesktopRule SelectedRule { get { return _rulesList.SelectedItems.Count == 0 ? null : _rulesList.SelectedItems[0].Tag as DesktopRule; } }
		private void RefreshRules(DesktopRule select = null) { _rulesList.BeginUpdate(); _rulesList.Items.Clear(); foreach(var rule in _rules.OrderBy(rule => rule.DesktopIndex).ThenBy(rule => rule.DisplayName)) { var item = new ListViewItem(new[] { (rule.DesktopIndex + 1).ToString(), rule.WindowTitle ?? "", rule.ProcessName ?? "", Localizer.L(rule.WindowTitleIsRegex ? "Regular expression" : "Exact") }) { Tag = rule }; _rulesList.Items.Add(item); if(rule == select) item.Selected = true; } _rulesList.EndUpdate(); if(_rulesList.Items.Count > 0 && _rulesList.SelectedItems.Count == 0) _rulesList.Items[0].Selected = true; ShowSelected(); }
		private void ShowSelected() { var rule = SelectedRule; _desktopNumber.Enabled = rule != null; _windowTitle.Enabled = rule != null; _applicationName.Enabled = rule != null; _useRegularExpression.Enabled = rule != null; _applyButton.Enabled = rule != null; _deleteButton.Enabled = rule != null; _desktopNumber.Value = rule == null ? 1 : rule.DesktopIndex + 1; _windowTitle.Text = rule == null ? "" : rule.WindowTitle; _applicationName.Text = rule == null ? "" : rule.ProcessName; _useRegularExpression.Checked = rule != null && rule.WindowTitleIsRegex; }
		private void AddRule() { var rule = new DesktopRule { DesktopIndex = 0, ProcessName = "", WindowTitle = "" }; _rules.Add(rule); RefreshRules(rule); _applicationName.Focus(); }
		private bool ApplySelected() { var rule = SelectedRule; if(rule == null) return true; var application = _applicationName.Text.Trim(); var title = _windowTitle.Text.Trim(); if(string.IsNullOrEmpty(application) || string.IsNullOrEmpty(title)) { MessageBox.Show(this, Localizer.L("An application name and window name are required."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; } if(_useRegularExpression.Checked) try { new Regex(title, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)); } catch(ArgumentException e) { MessageBox.Show(this, Localizer.L("The title regular expression is invalid.") + "\r\n\r\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; } var applicationChanged = !string.Equals(rule.ProcessName, application, StringComparison.OrdinalIgnoreCase); rule.DesktopIndex = Decimal.ToInt32(_desktopNumber.Value) - 1; rule.DesktopId = null; rule.ProcessName = application; rule.ApplicationNameIsOverride = rule.ApplicationNameIsOverride || applicationChanged; rule.WindowTitle = title; rule.WindowTitleIsRegex = _useRegularExpression.Checked; RefreshRules(rule); return true; }
		private void DeleteSelected() { var rule = SelectedRule; if(rule == null) return; _rules.Remove(rule); RefreshRules(); }
	}

	internal sealed class NameDialog : Form {
		private readonly TextBox _name;
		internal string Value { get { return _name.Text.Trim(); } }
		internal NameDialog(string title, string value) { Text = title; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(430, 120); FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false; _name = new TextBox { Dock = DockStyle.Top, Text = value, Margin = new Padding(12) }; var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft }; var ok = new Button { Text = Localizer.L("Save"), AutoSize = true, DialogResult = DialogResult.OK }; var cancel = new Button { Text = Localizer.L("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel }; buttons.Controls.Add(ok); buttons.Controls.Add(cancel); Controls.Add(_name); Controls.Add(buttons); AcceptButton = ok; CancelButton = cancel; FormClosing += (sender, e) => { if(DialogResult == DialogResult.OK && string.IsNullOrWhiteSpace(_name.Text)) { e.Cancel = true; MessageBox.Show(this, Localizer.L("A snapshot name is required."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); } }; }
	}

	internal sealed class RuleApplyPreviewForm : Form {
		internal RuleApplyPreviewForm(RuleRestorePreview preview) {
			Text = Localizer.L("Apply Desktop Rules"); StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(720, 520); MinimizeBox = false; MaximizeBox = false; FormBorderStyle = FormBorderStyle.FixedDialog;
			var summary = Localizer.IsChinese ? "只有唯一可靠匹配的窗口会在确认后移动。\r\n\r\n可移动：" + preview.Count(RuleRestoreStatus.CanRestore) + "\r\n已在目标桌面：" + preview.Count(RuleRestoreStatus.AlreadyCorrect) + "\r\n未找到：" + preview.Count(RuleRestoreStatus.NotFound) + "\r\n匹配不明确：" + preview.Count(RuleRestoreStatus.Ambiguous) + "\r\n\r\n不会启动或关闭应用程序。" : "Only uniquely matched open windows will be moved after confirmation.\r\n\r\nCan move: " + preview.Count(RuleRestoreStatus.CanRestore) + "\r\nAlready correct: " + preview.Count(RuleRestoreStatus.AlreadyCorrect) + "\r\nNot found: " + preview.Count(RuleRestoreStatus.NotFound) + "\r\nAmbiguous: " + preview.Count(RuleRestoreStatus.Ambiguous) + "\r\n\r\nNo application will be started or closed.";
			var info = new Label { Dock = DockStyle.Top, Height = 156, Padding = new Padding(12), Text = summary };
			var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true }; list.Columns.Add(Localizer.L("Status"), 130); list.Columns.Add(Localizer.L("Window"), 310); list.Columns.Add(Localizer.L("Reason"), 240);
			foreach(var item in preview.Items) list.Items.Add(new ListViewItem(new[] { StatusText(item.Status), item.Rule.DisplayName, Localizer.L(item.Reason) }));
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft }; var apply = new Button { Text = Localizer.L("Apply Desktop Rules"), AutoSize = true, DialogResult = DialogResult.OK }; var cancel = new Button { Text = Localizer.L("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel }; buttons.Controls.Add(apply); buttons.Controls.Add(cancel); Controls.Add(list); Controls.Add(info); Controls.Add(buttons); AcceptButton = apply; CancelButton = cancel;
		}
		private static string StatusText(RuleRestoreStatus status) { return Localizer.L(status == RuleRestoreStatus.CanRestore ? "Can restore" : status == RuleRestoreStatus.AlreadyCorrect ? "Already correct" : status == RuleRestoreStatus.NotFound ? "Not found" : status == RuleRestoreStatus.Ambiguous ? "Ambiguous match" : status.ToString()); }
	}

	internal sealed class SnapshotDetailsForm : Form { internal SnapshotDetailsForm(string name, string text) { Text = name; StartPosition = FormStartPosition.CenterParent; Size = new Size(680, 520); Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = text }); } }
}
