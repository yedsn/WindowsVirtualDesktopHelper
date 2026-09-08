using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	public sealed class WindowOverviewForm : Form {
		private readonly TextBox _searchBox;
		private readonly TreeView _windowsTree;
		private readonly Button _refreshButton;
		private readonly Button _activateButton;
		private readonly Button _closeButton;
		private readonly Label _statusLabel;
		private List<WindowOverviewItem> _items = new List<WindowOverviewItem>();
		private int _refreshVersion;
		private bool _isRefreshing;

		public WindowOverviewForm() {
			Text = "All Windows";
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(560, 380);
			Size = new Size(720, 540);
			ShowInTaskbar = true;

			var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 9, 10, 5) };
			_searchBox = new TextBox { Dock = DockStyle.Fill };
			_searchBox.AccessibleName = "Search windows";
			_refreshButton = new Button { Text = "Refresh", Dock = DockStyle.Right, Width = 86, Margin = new Padding(8, 0, 0, 0) };
			topPanel.Controls.Add(_searchBox);
			topPanel.Controls.Add(_refreshButton);

			_windowsTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, FullRowSelect = true, ShowLines = false };

			var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 7, 10, 7) };
			_statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
			_closeButton = new Button { Text = "Close Window", Dock = DockStyle.Right, Width = 104, Enabled = false };
			_activateButton = new Button { Text = "Activate", Dock = DockStyle.Right, Width = 86, Enabled = false, Margin = new Padding(0, 0, 8, 0) };
			bottomPanel.Controls.Add(_statusLabel);
			bottomPanel.Controls.Add(_closeButton);
			bottomPanel.Controls.Add(_activateButton);

			Controls.Add(_windowsTree);
			Controls.Add(bottomPanel);
			Controls.Add(topPanel);

			_searchBox.TextChanged += (sender, e) => PopulateTree();
			_refreshButton.Click += (sender, e) => RefreshSnapshot();
			_windowsTree.AfterSelect += (sender, e) => UpdateActionButtons();
			_windowsTree.NodeMouseDoubleClick += (sender, e) => ActivateSelectedWindow();
			_activateButton.Click += (sender, e) => ActivateSelectedWindow();
			_closeButton.Click += (sender, e) => CloseSelectedWindow();
			FormClosing += WindowOverviewForm_FormClosing;
		}

		public void RefreshSnapshot() {
			if(_isRefreshing) return;
			_isRefreshing = true;
			_refreshButton.Enabled = false;
			_statusLabel.Text = "Reading open windows...";
			var refreshVersion = ++_refreshVersion;
			var thread = new Thread(() => ReadSnapshot(refreshVersion)) { IsBackground = true };
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		private void ReadSnapshot(int refreshVersion) {
			List<WindowOverviewItem> items = null;
			Exception error = null;
			try {
				items = App.Instance.GetWindowOverviewSnapshot();
			} catch(Exception e) {
				error = e;
			}
			if(IsDisposed || !IsHandleCreated) return;
			try {
				BeginInvoke((Action)(() => ApplySnapshot(refreshVersion, items, error)));
			} catch(InvalidOperationException) { }
		}

		private void ApplySnapshot(int refreshVersion, List<WindowOverviewItem> items, Exception error) {
			if(refreshVersion != _refreshVersion) return;
			_isRefreshing = false;
			_refreshButton.Enabled = true;
			if(error != null) {
				_statusLabel.Text = "Could not read open windows: " + error.Message;
				return;
			}
			_items = items;
			_statusLabel.Text = _items.Count + " window" + (_items.Count == 1 ? "" : "s") + " found";
			PopulateTree();
		}

		private void PopulateTree() {
			var searchText = _searchBox.Text.Trim();
			var matchingItems = _items.Where(item => string.IsNullOrEmpty(searchText)
				|| item.Window.ProcessName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
				|| item.Window.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

			_windowsTree.BeginUpdate();
			_windowsTree.Nodes.Clear();
			foreach(var group in matchingItems.Where(item => item.DesktopIndex >= 0).GroupBy(item => item.DesktopIndex).OrderBy(group => group.Key)) {
				AddGroup("Desktop " + (group.Key + 1) + " (" + group.Count() + ")", group);
			}

			var unresolved = matchingItems.Where(item => item.DesktopIndex < 0).ToList();
			if(unresolved.Count > 0) AddGroup("Other windows (" + unresolved.Count + ")", unresolved);
			if(_windowsTree.Nodes.Count == 0) _windowsTree.Nodes.Add(new TreeNode("No matching application windows"));
			_windowsTree.ExpandAll();
			_windowsTree.EndUpdate();
			UpdateActionButtons();
		}

		private void AddGroup(string label, IEnumerable<WindowOverviewItem> items) {
			var groupNode = new TreeNode(label);
			foreach(var item in items.OrderBy(item => item.Window.ProcessName).ThenBy(item => item.Window.Title)) {
				groupNode.Nodes.Add(new TreeNode(item.DisplayName) { Tag = item });
			}
			_windowsTree.Nodes.Add(groupNode);
		}

		private WindowOverviewItem GetSelectedItem() {
			return _windowsTree.SelectedNode == null ? null : _windowsTree.SelectedNode.Tag as WindowOverviewItem;
		}

		private void UpdateActionButtons() {
			var isWindowSelected = GetSelectedItem() != null;
			_activateButton.Enabled = isWindowSelected;
			_closeButton.Enabled = isWindowSelected;
		}

		private void ActivateSelectedWindow() {
			var item = GetSelectedItem();
			if(item == null) return;
			_statusLabel.Text = App.Instance.ActivateOverviewWindow(item);
		}

		private void CloseSelectedWindow() {
			var item = GetSelectedItem();
			if(item == null) return;
			var result = MessageBox.Show(this, "Close \"" + item.DisplayName + "\"?", "Close Window", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
			if(result != DialogResult.Yes) return;
			_statusLabel.Text = App.Instance.CloseOverviewWindow(item);
			RefreshSnapshot();
		}

		private void WindowOverviewForm_FormClosing(object sender, FormClosingEventArgs e) {
			if(e.CloseReason == CloseReason.UserClosing) {
				_refreshVersion++;
				_isRefreshing = false;
				_refreshButton.Enabled = true;
				e.Cancel = true;
				Hide();
			}
		}
	}
}
