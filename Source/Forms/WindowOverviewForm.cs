using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	public sealed class WindowOverviewForm : Form {
		private const int GridColumnCount = 3;
		private readonly TextBox _searchBox;
		private readonly TableLayoutPanel _desktopGrid;
		private readonly Panel _gridHost;
		private readonly SkeletonLoadingPanel _loadingOverlay;
		private readonly Button _refreshButton;
		private readonly Button _activateButton;
		private readonly Button _closeButton;
		private readonly Label _statusLabel;
		private List<WindowOverviewItem> _items = new List<WindowOverviewItem>();
		private int _desktopCount = 1;
		private int _refreshVersion;
		private bool _isRefreshing;
		private WindowOverviewItem _selectedItem;

		public WindowOverviewForm() {
			Text = "All Windows";
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(860, 460);
			Size = new Size(1040, 680);
			ShowInTaskbar = true;

			var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 9, 10, 5) };
			_searchBox = new TextBox { Dock = DockStyle.Fill, AccessibleName = "Search windows" };
			_refreshButton = new Button { Text = "Refresh", Dock = DockStyle.Right, Width = 86 };
			topPanel.Controls.Add(_searchBox);
			topPanel.Controls.Add(_refreshButton);

			_gridHost = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Control };
			_desktopGrid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = false, Padding = new Padding(10), BackColor = SystemColors.Control, ColumnCount = GridColumnCount, GrowStyle = TableLayoutPanelGrowStyle.FixedSize };
			for(var column = 0; column < GridColumnCount; column++) _desktopGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / GridColumnCount));
			_loadingOverlay = CreateLoadingOverlay();
			_gridHost.Controls.Add(_desktopGrid);
			_gridHost.Controls.Add(_loadingOverlay);

			var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 7, 10, 7) };
			_statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
			_closeButton = new Button { Text = "Close Window", Dock = DockStyle.Right, Width = 104, Enabled = false };
			_activateButton = new Button { Text = "Activate", Dock = DockStyle.Right, Width = 86, Enabled = false };
			bottomPanel.Controls.Add(_statusLabel);
			bottomPanel.Controls.Add(_closeButton);
			bottomPanel.Controls.Add(_activateButton);

			Controls.Add(_gridHost);
			Controls.Add(bottomPanel);
			Controls.Add(topPanel);

			_searchBox.TextChanged += (sender, e) => PopulateGrid();
			_refreshButton.Click += (sender, e) => RefreshSnapshot();
			_activateButton.Click += (sender, e) => ActivateSelectedWindow();
			_closeButton.Click += (sender, e) => CloseSelectedWindow();
			FormClosing += WindowOverviewForm_FormClosing;
		}

		public void RefreshSnapshot() {
			if(_isRefreshing) return;
			_isRefreshing = true;
			_refreshButton.Enabled = false;
			_statusLabel.Text = "Reading open windows...";
			_loadingOverlay.DesktopCount = _desktopCount;
			_loadingOverlay.Visible = true;
			_loadingOverlay.BringToFront();
			var refreshVersion = ++_refreshVersion;
			var thread = new Thread(() => ReadSnapshot(refreshVersion)) { IsBackground = true };
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		private void ReadSnapshot(int refreshVersion) {
			List<WindowOverviewItem> items = null;
			var desktopCount = 1;
			Exception error = null;
			try {
				items = App.Instance.GetWindowOverviewSnapshot();
				desktopCount = App.Instance.GetWindowOverviewDesktopCount();
			} catch(Exception e) {
				error = e;
			}
			if(IsDisposed || !IsHandleCreated) return;
			try {
				BeginInvoke((Action)(() => ApplySnapshot(refreshVersion, items, desktopCount, error)));
			} catch(InvalidOperationException) { }
		}

		private void ApplySnapshot(int refreshVersion, List<WindowOverviewItem> items, int desktopCount, Exception error) {
			if(refreshVersion != _refreshVersion) return;
			_isRefreshing = false;
			_refreshButton.Enabled = true;
			_loadingOverlay.Visible = false;
			if(error != null) {
				_statusLabel.Text = "Could not read open windows: " + error.Message;
				return;
			}
			_items = items;
			_desktopCount = Math.Max(1, desktopCount);
			_selectedItem = null;
			_statusLabel.Text = _items.Count + " window" + (_items.Count == 1 ? "" : "s") + " found";
			PopulateGrid();
		}

		private void PopulateGrid() {
			if(_desktopGrid.ClientSize.Width <= 0) return;
			var searchText = _searchBox.Text.Trim();
			var matchingItems = _items.Where(item => string.IsNullOrEmpty(searchText)
				|| item.Window.ProcessName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
				|| item.Window.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

			_desktopGrid.SuspendLayout();
			_desktopGrid.Controls.Clear();
			_desktopGrid.RowStyles.Clear();
			var unresolved = matchingItems.Where(item => item.DesktopIndex < 0).ToList();
			var unresolved = matchingItems.Where(item => item.DesktopIndex < 0 && !item.IsShownOnAllDesktops).ToList();
			var cardCount = _desktopCount + (unresolved.Count > 0 ? 1 : 0);
			var rowCount = (int)Math.Ceiling(cardCount / (double)GridColumnCount);
			_desktopGrid.RowCount = rowCount;
			for(var row = 0; row < rowCount; row++) _desktopGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rowCount));
			var cardIndex = 0;
			for(var desktopIndex = 0; desktopIndex < _desktopCount; desktopIndex++) {
				AddDesktopCard("Desktop " + (desktopIndex + 1), matchingItems.Where(item => item.DesktopIndex == desktopIndex || item.IsShownOnAllDesktops), cardIndex++);
			}

			if(unresolved.Count > 0) AddDesktopCard("Other windows", unresolved, cardIndex++);
			while(cardIndex < rowCount * GridColumnCount) AddEmptyGridCell(cardIndex++);
			_desktopGrid.ResumeLayout();
			UpdateActionButtons();
		}

		private SkeletonLoadingPanel CreateLoadingOverlay() {
			return new SkeletonLoadingPanel { Dock = DockStyle.Fill, Visible = false, DesktopCount = _desktopCount };
		}

		private void AddDesktopCard(string title, IEnumerable<WindowOverviewItem> items, int cardIndex) {
			var cardItems = items.OrderBy(item => item.Window.ProcessName).ThenBy(item => item.Window.Title).ToList();
			var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 10), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
			var heading = new Label { Text = title + " (" + cardItems.Count + ")", Dock = DockStyle.Top, Height = 30, Padding = new Padding(9, 7, 0, 0), Font = new Font(Font, FontStyle.Bold), BackColor = Color.FromArgb(240, 243, 247) };
			var list = new ListView { Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.None, FullRowSelect = true, HideSelection = false, MultiSelect = false, BorderStyle = BorderStyle.None, SmallImageList = CreateImageList(cardItems) };
			list.Columns.Add("Window", -2);
			for(var i = 0; i < cardItems.Count; i++) list.Items.Add(new ListViewItem(cardItems[i].IsShownOnAllDesktops ? "[All desktops] " + cardItems[i].DisplayName : cardItems[i].DisplayName) { Tag = cardItems[i], ImageIndex = i });
			list.SelectedIndexChanged += (sender, e) => SelectListItem(list);
			list.DoubleClick += (sender, e) => ActivateSelectedWindow();
			card.Controls.Add(list);
			card.Controls.Add(heading);
			var row = cardIndex / GridColumnCount;
			var column = cardIndex % GridColumnCount;
			_desktopGrid.Controls.Add(card, column, row);
		}

		private void AddEmptyGridCell(int cardIndex) {
			var row = cardIndex / GridColumnCount;
			var column = cardIndex % GridColumnCount;
			_desktopGrid.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 10), BackColor = SystemColors.Control }, column, row);
		}

		private ImageList CreateImageList(List<WindowOverviewItem> items) {
			var images = new ImageList { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };
			foreach(var item in items) images.Images.Add(item.Window.Icon ?? SystemIcons.Application);
			return images;
		}

		private void SelectListItem(ListView list) {
			_selectedItem = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as WindowOverviewItem;
			UpdateActionButtons();
		}

		private void UpdateActionButtons() {
			var isWindowSelected = _selectedItem != null;
			_activateButton.Enabled = isWindowSelected;
			_closeButton.Enabled = isWindowSelected;
		}

		private void ActivateSelectedWindow() {
			if(_selectedItem == null) return;
			_statusLabel.Text = App.Instance.ActivateOverviewWindow(_selectedItem);
		}

		private void CloseSelectedWindow() {
			if(_selectedItem == null) return;
			var result = MessageBox.Show(this, "Close \"" + _selectedItem.DisplayName + "\"?", "Close Window", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
			if(result != DialogResult.Yes) return;
			_statusLabel.Text = App.Instance.CloseOverviewWindow(_selectedItem);
			RefreshSnapshot();
		}

		private void WindowOverviewForm_FormClosing(object sender, FormClosingEventArgs e) {
			if(e.CloseReason == CloseReason.UserClosing) {
				_refreshVersion++;
				_isRefreshing = false;
				_refreshButton.Enabled = true;
				_loadingOverlay.Visible = false;
				e.Cancel = true;
				Hide();
			}
		}

		private sealed class SkeletonLoadingPanel : Panel {
			private readonly System.Windows.Forms.Timer _timer;
			private bool _bright;
			internal int DesktopCount { get; set; }

			internal SkeletonLoadingPanel() {
				DoubleBuffered = true;
				_timer = new System.Windows.Forms.Timer { Interval = 450 };
				_timer.Tick += (sender, e) => { _bright = !_bright; Invalidate(); };
			}

			protected override void OnVisibleChanged(EventArgs e) {
				base.OnVisibleChanged(e);
				if(Visible) _timer.Start(); else _timer.Stop();
			}

			protected override void OnSizeChanged(EventArgs e) {
				base.OnSizeChanged(e);
				Invalidate();
			}

			protected override void OnPaint(PaintEventArgs e) {
				base.OnPaint(e);
				e.Graphics.Clear(SystemColors.Control);
				var cardCount = Math.Max(3, DesktopCount);
				var rowCount = (int)Math.Ceiling(cardCount / (double)GridColumnCount);
				var gap = 10;
				var cardWidth = Math.Max(1, (ClientSize.Width - 20 - ((GridColumnCount - 1) * gap)) / GridColumnCount);
				var rowHeight = Math.Max(1, (ClientSize.Height - 20 - ((rowCount - 1) * gap)) / rowCount);
				var fill = _bright ? Color.FromArgb(215, 220, 226) : Color.FromArgb(230, 233, 237);
				using(var brush = new SolidBrush(fill)) {
					for(var index = 0; index < cardCount; index++) {
						var row = index / GridColumnCount;
						var column = index % GridColumnCount;
						var x = 10 + (column * (cardWidth + gap));
						var y = 10 + (row * (rowHeight + gap));
						e.Graphics.FillRectangle(brush, x, y, cardWidth, rowHeight);
						e.Graphics.FillRectangle(brush, x + 12, y + 12, Math.Min(110, cardWidth - 24), 12);
						for(var line = 0; line < 4; line++) {
							var lineY = y + 46 + (line * 38);
							e.Graphics.FillEllipse(brush, x + 12, lineY, 20, 20);
							e.Graphics.FillRectangle(brush, x + 42, lineY + 3, Math.Max(24, cardWidth - 72 - (line * 18)), 14);
						}
					}
				}
			}
		}
	}
}
