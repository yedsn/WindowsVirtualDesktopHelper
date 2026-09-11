using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	internal sealed class WindowCleanupLockRulesForm : Form {
		private readonly ListView _rulesList;
		private readonly TextBox _applicationName;
		private readonly TextBox _windowTitle;
		private readonly CheckBox _windowTitleIsRegex;
		private readonly Button _applyButton;
		private readonly Button _deleteButton;
		private readonly Label _status;
		private readonly List<WindowCleanupLockRule> _rules;

		internal WindowCleanupLockRulesForm() {
			Text = Localizer.L("Lock Rules");
			StartPosition = FormStartPosition.CenterParent;
			MinimumSize = new Size(780, 480);
			Size = new Size(940, 620);
			ShowInTaskbar = false;
			_rules = App.Instance.WindowCleanupLocks.List();

			var help = new Label {
				Dock = DockStyle.Top,
				Height = 66,
				Padding = new Padding(12, 10, 12, 0),
				Text = Localizer.L("Edit lock rules to protect matching windows from one-click cleanup. Rules match the process name and window title without case sensitivity. Enable regular expressions when the title changes.")
			};
			_rulesList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
			_rulesList.Columns.Add(Localizer.L("Application"), 240);
			_rulesList.Columns.Add(Localizer.L("Window"), 470);
			_rulesList.Columns.Add(Localizer.L("Match type"), 130);

			var editor = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 106, Padding = new Padding(12, 8, 12, 8), ColumnCount = 4, RowCount = 2 };
			editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			editor.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			editor.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			_applicationName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
			_windowTitle = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
			_windowTitleIsRegex = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, Text = Localizer.L("Use regular expression") };
			_applyButton = new Button { Anchor = AnchorStyles.Right, Width = 72, Text = Localizer.L("Apply") };
			_deleteButton = new Button { Anchor = AnchorStyles.Right, Width = 72, Text = Localizer.L("Delete") };
			var editorActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
			editorActions.Controls.Add(_applyButton);
			editorActions.Controls.Add(_deleteButton);
			editor.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Localizer.L("Application") }, 0, 0);
			editor.Controls.Add(_applicationName, 1, 0);
			editor.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = Localizer.L("Window") }, 2, 0);
			editor.Controls.Add(_windowTitle, 3, 0);
			editor.Controls.Add(_windowTitleIsRegex, 0, 1);
			editor.SetColumnSpan(_windowTitleIsRegex, 2);
			editor.Controls.Add(editorActions, 3, 1);

			_status = new Label { Dock = DockStyle.Bottom, Height = 26, Padding = new Padding(12, 4, 12, 0), TextAlign = ContentAlignment.MiddleLeft };
			var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(12, 8, 12, 8), FlowDirection = FlowDirection.RightToLeft };
			var save = AddButton(buttons, Localizer.L("Save"), (sender, e) => SaveRules());
			var cancel = new Button { Text = Localizer.L("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
			var add = AddButton(buttons, Localizer.L("New Lock Rule"), (sender, e) => AddRule());
			buttons.Controls.Add(cancel);

			Controls.Add(_rulesList);
			Controls.Add(editor);
			Controls.Add(_status);
			Controls.Add(buttons);
			Controls.Add(help);
			_rulesList.SelectedIndexChanged += (sender, e) => ShowSelectedRule();
			_applyButton.Click += (sender, e) => ApplySelectedRule();
			_deleteButton.Click += (sender, e) => DeleteSelectedRule();
			AcceptButton = save;
			CancelButton = cancel;

			RefreshRules();
		}

		private static Button AddButton(Control parent, string text, EventHandler click) {
			var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 0, 6, 0) };
			if(click != null) button.Click += click;
			parent.Controls.Add(button);
			return button;
		}

		private WindowCleanupLockRule SelectedRule {
			get { return _rulesList.SelectedItems.Count == 0 ? null : _rulesList.SelectedItems[0].Tag as WindowCleanupLockRule; }
		}

		private void RefreshRules(WindowCleanupLockRule selectRule = null) {
			_rulesList.BeginUpdate();
			_rulesList.Items.Clear();
			foreach(var rule in _rules.OrderBy(rule => rule.ProcessName, StringComparer.OrdinalIgnoreCase).ThenBy(rule => rule.WindowTitle, StringComparer.OrdinalIgnoreCase)) {
				var item = new ListViewItem(new[] { rule.ProcessName ?? "", rule.WindowTitle ?? "", Localizer.L(rule.WindowTitleIsRegex ? "Regular expression" : "Exact") }) { Tag = rule };
				_rulesList.Items.Add(item);
				if(rule == selectRule) item.Selected = true;
			}
			_rulesList.EndUpdate();
			if(selectRule == null && _rulesList.Items.Count > 0) _rulesList.Items[0].Selected = true;
			ShowSelectedRule();
			_status.Text = _rules.Count == 0 ? Localizer.L("No lock rules saved.") : (Localizer.IsChinese ? "已配置 " + _rules.Count + " 条锁定规则。" : _rules.Count + " lock rule" + (_rules.Count == 1 ? "" : "s") + " configured.");
		}

		private void ShowSelectedRule() {
			var rule = SelectedRule;
			_applicationName.Text = rule == null ? "" : rule.ProcessName ?? "";
			_windowTitle.Text = rule == null ? "" : rule.WindowTitle ?? "";
			_windowTitleIsRegex.Checked = rule != null && rule.WindowTitleIsRegex;
			_applicationName.Enabled = rule != null;
			_windowTitle.Enabled = rule != null;
			_windowTitleIsRegex.Enabled = rule != null;
			_applyButton.Enabled = rule != null;
			_deleteButton.Enabled = rule != null;
		}

		private void AddRule() {
			var rule = new WindowCleanupLockRule { ProcessName = "", WindowTitle = "", WindowTitleIsRegex = false };
			_rules.Add(rule);
			RefreshRules(rule);
			_applicationName.Focus();
		}

		private bool ApplySelectedRule() {
			var rule = SelectedRule;
			if(rule == null) return true;
			var applicationName = _applicationName.Text.Trim();
			var windowTitle = _windowTitle.Text.Trim();
			if(string.IsNullOrEmpty(applicationName)) {
				MessageBox.Show(this, Localizer.L("An application name is required."), Localizer.L("Lock Rules"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}
			if(_windowTitleIsRegex.Checked && string.IsNullOrEmpty(windowTitle)) {
				MessageBox.Show(this, Localizer.L("A window name is required."), Localizer.L("Lock Rules"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}
			if(_windowTitleIsRegex.Checked) try {
				new Regex(windowTitle, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
			} catch(ArgumentException e) {
				MessageBox.Show(this, Localizer.L("The title regular expression is invalid.") + "\r\n\r\n" + e.Message, Localizer.L("Lock Rules"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}
			rule.ProcessName = applicationName;
			rule.WindowTitle = windowTitle;
			rule.WindowTitleIsRegex = _windowTitleIsRegex.Checked;
			_rulesList.SelectedItems[0].SubItems[0].Text = rule.ProcessName;
			_rulesList.SelectedItems[0].SubItems[1].Text = rule.WindowTitle;
			_rulesList.SelectedItems[0].SubItems[2].Text = Localizer.L(rule.WindowTitleIsRegex ? "Regular expression" : "Exact");
			return true;
		}

		private void DeleteSelectedRule() {
			var rule = SelectedRule;
			if(rule == null) return;
			if(MessageBox.Show(this, Localizer.L("Remove this lock rule? Matching windows will no longer be protected."), Localizer.L("Lock Rules"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
			var selectedIndex = _rulesList.SelectedIndices[0];
			_rules.Remove(rule);
			RefreshRules();
			if(_rulesList.Items.Count > 0) _rulesList.Items[Math.Min(selectedIndex, _rulesList.Items.Count - 1)].Selected = true;
		}

		private void SaveRules() {
			if(!ApplySelectedRule()) return;
			try {
				App.Instance.WindowCleanupLocks.ReplaceAll(_rules);
				DialogResult = DialogResult.OK;
			} catch(Exception e) {
				_status.Text = Localizer.L("Could not save lock rules.");
				MessageBox.Show(this, _status.Text + "\r\n\r\n" + e.Message, Localizer.L("Lock Rules"), MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}
