using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	public sealed class WindowOverviewForm : Form {
		private const int GridColumnCount = 3;
		private readonly TextBox _searchBox;
		private readonly BufferedTableLayoutPanel _desktopGrid;
		private readonly Panel _gridHost;
		private readonly SkeletonLoadingPanel _loadingOverlay;
		private readonly Button _refreshButton;
		private readonly Button _snapshotsButton;
		private readonly Button _updateDesktopRulesButton;
		private readonly Button _manageDesktopRulesButton;
		private readonly Button _applyDesktopRulesButton;
		private readonly Button _manageLockRulesButton;
		private readonly Button _lockAllButton;
		private readonly Button _cleanupButton;
		private readonly Button _activateButton;
		private readonly Button _closeButton;
		private readonly Label _statusLabel;
		private readonly System.Windows.Forms.Timer _searchDebounceTimer;
		private List<WindowOverviewItem> _items = new List<WindowOverviewItem>();
		private List<string> _desktopNames = new List<string>();
		private readonly List<ListView> _windowLists = new List<ListView>();
		private int _desktopCount = 1;
		private int _refreshVersion;
		private bool _isRefreshing;
		private bool _isBulkActionRunning;
		private bool _hasSnapshot;
		private bool _isClearingSearch;
		private bool _isUpdatingSelection;
		private readonly List<Keys> _pendingNavigation = new List<Keys>();
		private IntPtr _selectionHandle = IntPtr.Zero;
		private int _selectionListIndex = -1;
		private int _selectionItemIndex = -1;
		private WindowOverviewItem _selectedItem;

		public WindowOverviewForm() {
			Text = Localizer.L("All Windows");
			StartPosition = FormStartPosition.CenterScreen;
			MinimumSize = new Size(1100, 460);
			Size = new Size(1280, 800);
			ShowInTaskbar = true;
			KeyPreview = true;

			var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 9, 10, 5) };
			_searchBox = new TextBox { Dock = DockStyle.Fill, AccessibleName = Localizer.L("Search windows") };
			_refreshButton = new Button { Text = Localizer.L("Refresh"), Dock = DockStyle.Right, Width = 86 };
			_snapshotsButton = new Button { Text = Localizer.L("Snapshot"), Dock = DockStyle.Right, Width = 88 };
			_searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 250 };
			topPanel.Controls.Add(_searchBox);
			topPanel.Controls.Add(_refreshButton);
			topPanel.Controls.Add(_snapshotsButton);

			_gridHost = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Control };
			_desktopGrid = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = false, Padding = new Padding(10), BackColor = SystemColors.Control, ColumnCount = GridColumnCount, GrowStyle = TableLayoutPanelGrowStyle.FixedSize };
			for(var column = 0; column < GridColumnCount; column++) _desktopGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / GridColumnCount));
			_loadingOverlay = CreateLoadingOverlay();
			_gridHost.Controls.Add(_desktopGrid);
			_gridHost.Controls.Add(_loadingOverlay);
			_desktopGrid.Visible = false;
			_loadingOverlay.Visible = true;

			var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 7, 10, 7) };
			_statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
			var selectedWindowActions = new Panel { Dock = DockStyle.Right, Width = 208 };
			_closeButton = new Button { Text = Localizer.L("Close Window"), Dock = DockStyle.Right, Width = 104, Enabled = false };
			_activateButton = new Button { Text = Localizer.L("Activate Window"), Dock = DockStyle.Right, Width = 104, Enabled = false };
			selectedWindowActions.Controls.Add(_activateButton);
			selectedWindowActions.Controls.Add(_closeButton);
			var selectedWindowActionsGap = new Panel { Dock = DockStyle.Right, Width = 10 };
			var desktopRuleActionsGap = new Panel { Dock = DockStyle.Right, Width = 10 };
			var desktopRuleActions = new Panel { Dock = DockStyle.Right, Width = 384 };
			var cleanupActions = new Panel { Dock = DockStyle.Right, Width = 380 };
			_lockAllButton = new Button { Text = Localizer.L("Lock All"), Dock = DockStyle.Left, Width = 88 };
			_manageLockRulesButton = new Button { Text = Localizer.L("Manage Lock Rules"), Dock = DockStyle.Left, Width = 136 };
			_updateDesktopRulesButton = new Button { Text = Localizer.L("Update Desktop Rules"), Dock = DockStyle.Left, Width = 126 };
			_manageDesktopRulesButton = new Button { Text = Localizer.L("Manage Desktop Rules"), Dock = DockStyle.Left, Width = 132 };
			_applyDesktopRulesButton = new Button { Text = Localizer.L("Apply Desktop Rules"), Dock = DockStyle.Left, Width = 126 };
			var cleanupPanel = new Panel { Dock = DockStyle.Fill };
			_cleanupButton = new Button { Text = string.Format(Localizer.L("One-click Cleanup ({0})"), 0), Dock = DockStyle.Fill };
			cleanupPanel.Controls.Add(_cleanupButton);
			cleanupActions.Controls.Add(cleanupPanel);
			cleanupActions.Controls.Add(_manageLockRulesButton);
			cleanupActions.Controls.Add(_lockAllButton);
			desktopRuleActions.Controls.Add(_applyDesktopRulesButton);
			desktopRuleActions.Controls.Add(_manageDesktopRulesButton);
			desktopRuleActions.Controls.Add(_updateDesktopRulesButton);
			bottomPanel.Controls.Add(_statusLabel);
			bottomPanel.Controls.Add(selectedWindowActions);
			bottomPanel.Controls.Add(selectedWindowActionsGap);
			bottomPanel.Controls.Add(desktopRuleActions);
			bottomPanel.Controls.Add(desktopRuleActionsGap);
			bottomPanel.Controls.Add(cleanupActions);

			Controls.Add(_gridHost);
			Controls.Add(bottomPanel);
			Controls.Add(topPanel);

			_searchBox.TextChanged += (sender, e) => {
				if(_isClearingSearch) return;
				_searchDebounceTimer.Stop();
				_searchDebounceTimer.Start();
			};
			_searchDebounceTimer.Tick += (sender, e) => {
				_searchDebounceTimer.Stop();
				PopulateGrid();
			};
			_refreshButton.Click += (sender, e) => RefreshSnapshot();
			_snapshotsButton.Click += (sender, e) => App.Instance.ShowDesktopLayoutSnapshots();
			_updateDesktopRulesButton.Click += (sender, e) => UpdateDesktopRules();
			_manageDesktopRulesButton.Click += (sender, e) => ManageDesktopRules();
			_applyDesktopRulesButton.Click += (sender, e) => BeginApplyDesktopRules();
			_manageLockRulesButton.Click += (sender, e) => ManageLockRules();
			_lockAllButton.Click += (sender, e) => LockAllWindows();
			_cleanupButton.Click += (sender, e) => BeginCleanupAllDesktops();
			_activateButton.Click += (sender, e) => ActivateSelectedWindow();
			_closeButton.Click += (sender, e) => CloseSelectedWindow();
			KeyDown += WindowOverviewForm_KeyDown;
			FormClosing += WindowOverviewForm_FormClosing;
		}

		public void UpdateWindowIcon(string theme, int dpi) {
			Icon = Util.Icons.GenerateDesktopManagerIcon(theme, dpi);
		}

		public void ApplyLocalizedText() {
			Text = Localizer.L(Text);
			_searchBox.AccessibleName = Localizer.L(_searchBox.AccessibleName);
			_refreshButton.Text = Localizer.L(_refreshButton.Text);
			_snapshotsButton.Text = Localizer.L(_snapshotsButton.Text);
			_updateDesktopRulesButton.Text = Localizer.L(_updateDesktopRulesButton.Text);
			_manageDesktopRulesButton.Text = Localizer.L(_manageDesktopRulesButton.Text);
			_applyDesktopRulesButton.Text = Localizer.L(_applyDesktopRulesButton.Text);
			_manageLockRulesButton.Text = Localizer.L(_manageLockRulesButton.Text);
			_lockAllButton.Text = Localizer.L(_lockAllButton.Text);
			_closeButton.Text = Localizer.L(_closeButton.Text);
			_activateButton.Text = Localizer.L(_activateButton.Text);
			UpdateCleanupTargetCount();
			if(_hasSnapshot) PopulateGrid();
		}

		public void FocusSearchBox() {
			if(IsDisposed || !IsHandleCreated) return;
			try {
				BeginInvoke((Action)(() => {
					if(IsDisposed || !Visible) return;
					ActiveControl = _searchBox;
					_searchBox.Focus();
				}));
			} catch(InvalidOperationException) { }
		}

		public void RefreshSnapshot() {
			if(_isRefreshing) return;
			_isRefreshing = true;
			_refreshButton.Enabled = false;
			_statusLabel.Text = Localizer.IsChinese ? "正在读取打开的窗口..." : "Reading open windows...";
			if(!_hasSnapshot) {
				_loadingOverlay.DesktopCount = _desktopCount;
				_desktopGrid.Visible = false;
				_loadingOverlay.Visible = true;
				_loadingOverlay.BringToFront();
			}
			var refreshVersion = ++_refreshVersion;
			var thread = new Thread(() => ReadSnapshot(refreshVersion)) { IsBackground = true };
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		private void ManageLockRules() {
			using(var dialog = new WindowCleanupLockRulesForm()) {
				if(dialog.ShowDialog(this) == DialogResult.OK) RefreshSnapshot();
			}
		}

		private void UpdateDesktopRules() {
			RunBulkAction(() => App.Instance.DesktopRules.UpdateFromOpenWindows(), result => {
				_statusLabel.Text = Localizer.IsChinese ? "桌面规则已更新：更新 " + result.UpdatedCount + " 条，新增 " + result.AddedCount + " 条。" : "Desktop rules updated: " + result.UpdatedCount + " updated, " + result.AddedCount + " added.";
				if(result.Conflicts.Count > 0) _statusLabel.Text += Localizer.IsChinese ? " " + result.Conflicts.Count + " 项匹配存在歧义，请在管理桌面规则中处理。" : " " + result.Conflicts.Count + " ambiguous mapping(s) need review.";
				RefreshSnapshot();
			});
		}

		private void ManageDesktopRules() {
			using(var dialog = new DesktopRulesForm()) {
				if(dialog.ShowDialog(this) == DialogResult.OK) RefreshSnapshot();
			}
		}

		private void BeginApplyDesktopRules() {
			RunBulkAction(() => App.Instance.DesktopRules.Analyze(), preview => {
				using(var dialog = new RuleApplyPreviewForm(preview)) {
					if(dialog.ShowDialog(this) != DialogResult.OK) return;
				}
				RunBulkAction(() => App.Instance.DesktopRules.Apply(preview, null), items => {
					_statusLabel.Text = Localizer.IsChinese ? "桌面规则应用完成：移动 " + items.Count(item => item.Status == RuleRestoreStatus.Moved) + " 个窗口。" : "Desktop rules applied: " + items.Count(item => item.Status == RuleRestoreStatus.Moved) + " window(s) moved.";
					RefreshSnapshot();
				});
			});
		}

		private void ReadSnapshot(int refreshVersion) {
			List<WindowOverviewItem> items = null;
			List<string> desktopNames = null;
			var desktopCount = 1;
			Exception error = null;
			try {
				items = App.Instance.GetWindowOverviewSnapshot();
				desktopNames = App.Instance.GetWindowOverviewDesktopNames();
				desktopCount = App.Instance.GetWindowOverviewDesktopCount();
			} catch(Exception e) {
				error = e;
			}
			if(IsDisposed || !IsHandleCreated) return;
			try {
				BeginInvoke((Action)(() => ApplySnapshot(refreshVersion, items, desktopNames, desktopCount, error)));
			} catch(InvalidOperationException) { }
		}

		private void ApplySnapshot(int refreshVersion, List<WindowOverviewItem> items, List<string> desktopNames, int desktopCount, Exception error) {
			if(refreshVersion != _refreshVersion) return;
			_isRefreshing = false;
			_refreshButton.Enabled = true;
			if(error != null) {
				if(!_hasSnapshot) {
					_loadingOverlay.Visible = false;
					_desktopGrid.Visible = true;
				}
				_statusLabel.Text = (Localizer.IsChinese ? "无法读取打开的窗口：" : "Could not read open windows: ") + error.Message;
				return;
			}
			_items = items;
			_desktopNames = desktopNames ?? new List<string>();
			_desktopCount = Math.Max(1, desktopCount);
			_statusLabel.Text = Localizer.IsChinese ? "找到 " + _items.Count + " 个窗口" : _items.Count + " window" + (_items.Count == 1 ? "" : "s") + " found";
			PopulateGrid();
			_hasSnapshot = true;
			_loadingOverlay.Visible = false;
			_desktopGrid.Visible = true;
			ApplyPendingNavigation();
		}

		private void PopulateGrid() {
			if(_desktopGrid.ClientSize.Width <= 0) return;
			CaptureSelectionPosition();
			var searchText = _searchBox.Text.Trim();
			var matchingItems = _items.Where(item => string.IsNullOrEmpty(searchText)
				|| item.Window.ProcessName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
				|| item.Window.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

			_desktopGrid.BeginGridUpdate();
			try {
				_desktopGrid.Controls.Clear();
				_windowLists.Clear();
				_desktopGrid.RowStyles.Clear();
				var unresolved = matchingItems.Where(item => item.DesktopIndex < 0 && !item.IsShownOnAllDesktops).ToList();
				var cardCount = _desktopCount + (unresolved.Count > 0 ? 1 : 0);
				var rowCount = (int)Math.Ceiling(cardCount / (double)GridColumnCount);
				_desktopGrid.RowCount = rowCount;
				for(var row = 0; row < rowCount; row++) _desktopGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rowCount));
				var cardIndex = 0;
				for(var desktopIndex = 0; desktopIndex < _desktopCount; desktopIndex++) {
					AddDesktopCard(GetDesktopTitle(desktopIndex), matchingItems.Where(item => item.DesktopIndex == desktopIndex || item.IsShownOnAllDesktops), cardIndex++, desktopIndex);
				}

				if(unresolved.Count > 0) AddDesktopCard(Localizer.L("Other windows"), unresolved, cardIndex++, -1);
				while(cardIndex < rowCount * GridColumnCount) AddEmptyGridCell(cardIndex++);
			} finally {
				_desktopGrid.EndGridUpdate();
			}
			RestoreSelectionPosition();
			UpdateActionButtons();
			UpdateCleanupTargetCount();
		}

		private void UpdateCleanupTargetCount() {
			var count = _items.Count(item => IsCleanupEligible(item) && !item.IsCleanupProtected);
			_cleanupButton.Text = string.Format(Localizer.L("One-click Cleanup ({0})"), count);
			_cleanupButton.ForeColor = count > 0 ? Color.Firebrick : SystemColors.ControlText;
			_cleanupButton.AccessibleName = _cleanupButton.Text;
		}

		private static bool IsCleanupEligible(WindowOverviewItem item) {
			return item != null && (item.DesktopIndex >= 0 || item.IsShownOnAllDesktops);
		}

		private void CaptureSelectionPosition() {
			_selectionHandle = IntPtr.Zero;
			_selectionListIndex = -1;
			_selectionItemIndex = -1;
			_selectedItem = null;
			for(var listIndex = 0; listIndex < _windowLists.Count; listIndex++) {
				var list = _windowLists[listIndex];
				if(list.SelectedItems.Count == 0) continue;
				_selectionListIndex = listIndex;
				_selectionItemIndex = list.SelectedIndices[0];
				var item = list.SelectedItems[0].Tag as WindowOverviewItem;
				if(item != null) _selectionHandle = item.Window.Handle;
				return;
			}
		}

		private void RestoreSelectionPosition() {
			if(_selectionListIndex < 0 || _windowLists.Count == 0) return;
			var targetListIndex = Math.Min(_selectionListIndex, _windowLists.Count - 1);
			var targetList = _windowLists[targetListIndex];
			if(_selectionHandle != IntPtr.Zero) {
				for(var itemIndex = 0; itemIndex < targetList.Items.Count; itemIndex++) {
					var item = targetList.Items[itemIndex].Tag as WindowOverviewItem;
					if(item != null && item.Window.Handle == _selectionHandle) {
						SelectListItem(targetList, itemIndex);
						return;
					}
				}
			}
			if(targetList.Items.Count > 0) SelectListItem(targetList, Math.Min(_selectionItemIndex, targetList.Items.Count - 1));
		}

		private SkeletonLoadingPanel CreateLoadingOverlay() {
			return new SkeletonLoadingPanel { Dock = DockStyle.Fill, Visible = false, DesktopCount = _desktopCount };
		}

		private string GetDesktopTitle(int desktopIndex) {
			var defaultTitle = Localizer.Desktop(desktopIndex + 1);
			if(desktopIndex < 0 || desktopIndex >= _desktopNames.Count || string.IsNullOrEmpty(_desktopNames[desktopIndex]) || _desktopNames[desktopIndex] == defaultTitle) return defaultTitle;
			return defaultTitle + " - " + _desktopNames[desktopIndex];
		}

		private void AddDesktopCard(string title, IEnumerable<WindowOverviewItem> items, int cardIndex, int desktopIndex) {
			var cardItems = items.OrderBy(item => item.Window.ProcessName).ThenBy(item => item.Window.Title).ToList();
			var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 10), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
			var heading = new Label { Text = title + " (" + cardItems.Count + ")", Dock = DockStyle.Top, Height = 30, Padding = new Padding(9, 7, 0, 0), Font = new Font(Font, FontStyle.Bold), BackColor = Color.FromArgb(240, 243, 247) };
			var list = new ListView { Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.None, FullRowSelect = true, HideSelection = false, MultiSelect = false, BorderStyle = BorderStyle.None, SmallImageList = CreateImageList(cardItems), StateImageList = CreateCleanupLockImageList(), ShowItemToolTips = true };
			list.AllowDrop = true;
			list.Columns.Add(Localizer.L("Window"), -2);
			for(var i = 0; i < cardItems.Count; i++) {
				var item = new ListViewItem(cardItems[i].DisplayName) { Tag = cardItems[i], ImageIndex = i, StateImageIndex = cardItems[i].IsCleanupProtected ? 2 : 1, ForeColor = GetWindowRowColor(cardItems[i]) };
				item.ToolTipText = Localizer.L(cardItems[i].IsCleanupProtected ? "Locked - protected from one-click cleanup" : "Unlocked - included in one-click cleanup");
				list.Items.Add(item);
			}
			list.SelectedIndexChanged += (sender, e) => SelectListItem(list);
			list.MouseUp += (sender, e) => ToggleCleanupProtectionFromStateImage(list, e);
			list.DoubleClick += (sender, e) => ActivateSelectedWindow(true);
			list.ItemDrag += (sender, e) => BeginWindowDrag(e.Item as ListViewItem);
			AttachDropHandlers(card, heading, list, desktopIndex);
			card.Controls.Add(list);
			card.Controls.Add(heading);
			var row = cardIndex / GridColumnCount;
			var column = cardIndex % GridColumnCount;
			_desktopGrid.Controls.Add(card, column, row);
			_windowLists.Add(list);
		}

		private void AttachDropHandlers(Control card, Control heading, Control list, int targetDesktopIndex) {
			foreach(var target in new[] { card, heading, list }) {
				target.AllowDrop = true;
				target.DragEnter += (sender, e) => UpdateDragEffect(e, targetDesktopIndex);
				target.DragOver += (sender, e) => UpdateDragEffect(e, targetDesktopIndex);
				target.DragDrop += (sender, e) => DropWindow(e, targetDesktopIndex);
			}
		}

		private void BeginWindowDrag(ListViewItem row) {
			var item = row == null ? null : row.Tag as WindowOverviewItem;
			if(item == null || item.IsShownOnAllDesktops) return;
			DoDragDrop(item, DragDropEffects.Move);
		}

		private void UpdateDragEffect(DragEventArgs e, int targetDesktopIndex) {
			var item = e.Data.GetData(typeof(WindowOverviewItem)) as WindowOverviewItem;
			e.Effect = item != null && targetDesktopIndex >= 0 && !item.IsShownOnAllDesktops && item.DesktopIndex != targetDesktopIndex ? DragDropEffects.Move : DragDropEffects.None;
		}

		private void DropWindow(DragEventArgs e, int targetDesktopIndex) {
			var item = e.Data.GetData(typeof(WindowOverviewItem)) as WindowOverviewItem;
			if(item == null || targetDesktopIndex < 0 || item.IsShownOnAllDesktops || item.DesktopIndex == targetDesktopIndex) return;
			_statusLabel.Text = Localizer.IsChinese ? "正在移动窗口..." : "Moving window...";
			var message = App.Instance.MoveOverviewWindow(item, targetDesktopIndex);
			_statusLabel.Text = message;
			if(message.StartsWith(Localizer.L("Window moved to Desktop "), StringComparison.Ordinal)) RefreshSnapshot();
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

		private ImageList CreateCleanupLockImageList() {
			var images = new ImageList { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };
			var empty = new Bitmap(20, 20);
			images.Images.Add(empty);
			images.Images.Add(CreateCleanupLockImage(false));
			images.Images.Add(CreateCleanupLockImage(true));
			return images;
		}

		private static Bitmap CreateCleanupLockImage(bool locked) {
			var bitmap = new Bitmap(20, 20);
			using(var graphics = Graphics.FromImage(bitmap)) using(var pen = new Pen(locked ? Color.FromArgb(35, 112, 55) : Color.FromArgb(100, 100, 100), 2)) using(var brush = new SolidBrush(locked ? Color.FromArgb(75, 157, 93) : Color.FromArgb(220, 220, 220))) {
				graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				graphics.FillRectangle(brush, 5, 9, 10, 8);
				graphics.DrawRectangle(pen, 5, 9, 10, 8);
				if(locked) graphics.DrawArc(pen, 6, 3, 8, 10, 180, 180);
				else {
					graphics.DrawArc(pen, 6, 3, 8, 10, 180, 135);
					graphics.DrawLine(pen, 13, 8, 16, 5);
				}
			}
			return bitmap;
		}

		private static Color GetWindowRowColor(WindowOverviewItem item) {
			if(item.IsCleanupProtected) return Color.FromArgb(35, 112, 55);
			return item.IsShownOnAllDesktops ? Color.DimGray : SystemColors.WindowText;
		}

		private void ToggleCleanupProtectionFromStateImage(ListView list, MouseEventArgs e) {
			var hit = list.HitTest(e.Location);
			if(hit.Item == null || (hit.Location & ListViewHitTestLocations.StateImage) == 0) return;
			var overviewItem = hit.Item.Tag as WindowOverviewItem;
			if(overviewItem == null) return;
			var isProtected = App.Instance.ToggleOverviewWindowCleanupProtection(overviewItem);
			UpdateCleanupProtectionDisplay(overviewItem.Window.Handle, isProtected);
			_statusLabel.Text = Localizer.L(isProtected ? "Window locked for cleanup." : "Window unlocked for cleanup.");
			UpdateCleanupTargetCount();
		}

		private void UpdateCleanupProtectionDisplay(IntPtr windowHandle, bool isProtected) {
			foreach(var item in _items.Where(item => item.Window.Handle == windowHandle)) item.SetCleanupProtection(isProtected);
			foreach(var list in _windowLists) foreach(ListViewItem row in list.Items) {
				var overviewItem = row.Tag as WindowOverviewItem;
				if(overviewItem == null || overviewItem.Window.Handle != windowHandle) continue;
				overviewItem.SetCleanupProtection(isProtected);
				row.StateImageIndex = isProtected ? 2 : 1;
				row.ForeColor = GetWindowRowColor(overviewItem);
				row.ToolTipText = Localizer.L(isProtected ? "Locked - protected from one-click cleanup" : "Unlocked - included in one-click cleanup");
			}
		}

		private void SelectListItem(ListView list) {
			if(_isUpdatingSelection) return;
			_isUpdatingSelection = true;
			try {
				foreach(var otherList in _windowLists.Where(otherList => otherList != list)) {
					foreach(ListViewItem item in otherList.SelectedItems) item.Selected = false;
				}
			} finally {
				_isUpdatingSelection = false;
			}
			_selectedItem = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as WindowOverviewItem;
			UpdateActionButtons();
		}

		private void UpdateActionButtons() {
			var isWindowSelected = _selectedItem != null;
			_activateButton.Enabled = isWindowSelected;
			_closeButton.Enabled = isWindowSelected;
		}

		private void ActivateSelectedWindow(bool closeOverviewAfterActivation = false) {
			if(_selectedItem == null) return;
			_statusLabel.Text = App.Instance.ActivateOverviewWindow(_selectedItem);
			if(closeOverviewAfterActivation) Close();
		}

		private void CloseSelectedWindow() {
			if(_selectedItem == null) return;
			var result = MessageBox.Show(this, (Localizer.IsChinese ? "关闭 \"" : "Close \"") + _selectedItem.DisplayName + "\"?", Localizer.L("Close Window"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
			if(result != DialogResult.Yes) return;
			_statusLabel.Text = App.Instance.CloseOverviewWindow(_selectedItem);
			RefreshSnapshot();
		}

		private void LockAllWindows() {
			RunBulkAction(() => App.Instance.PrepareOverviewLockAll(), batch => {
				_statusLabel.Text = string.Format(Localizer.L("Locked {0} windows on all desktops."), batch.Windows.Count);
				RefreshSnapshot();
			});
		}

		private void BeginCleanupAllDesktops() {
			RunBulkAction(() => App.Instance.PrepareOverviewCleanup(), batch => {
				if(batch.Windows.Count == 0) {
					_statusLabel.Text = Localizer.L("No unprotected windows on any desktop.");
					return;
				}
				if(!ConfirmCleanupTargets(batch)) return;
				RunBulkAction(() => App.Instance.ExecuteOverviewCleanup(batch), result => {
					_statusLabel.Text = string.Format(Localizer.L("Cleanup complete: {0} close requests sent; {1} could not be sent; {2} require administrator permission."), result.RequestedCloseCount, result.FailedCloseCount, result.AccessDeniedCloseCount);
					RefreshSnapshot();
				});
			});
		}

		private bool ConfirmCleanupTargets(WindowCleanupBatch batch) {
			using(var dialog = new Form { Text = Localizer.L("One-click Cleanup"), StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(760, 500), MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog, ShowInTaskbar = false }) {
				var header = new Label {
					Dock = DockStyle.Top,
					Height = 58,
					Padding = new Padding(12, 12, 12, 4),
					Text = string.Format(Localizer.L("Review {0} windows across all desktops before cleanup."), batch.Windows.Count)
				};
				var targets = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HideSelection = false };
				targets.Columns.Add(Localizer.L("Application"), 210);
				targets.Columns.Add(Localizer.L("Window"), 490);
				foreach(var window in batch.Windows) targets.Items.Add(new ListViewItem(new[] { window.ProcessName, window.Title }));

				var buttons = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
				var cancel = new Button { Text = Localizer.L("Cancel"), DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 88 };
				var confirm = new Button { Text = Localizer.L("Confirm Cleanup"), DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 132 };
				buttons.Controls.Add(cancel);
				buttons.Controls.Add(confirm);
				dialog.Controls.Add(targets);
				dialog.Controls.Add(buttons);
				dialog.Controls.Add(header);
				dialog.AcceptButton = confirm;
				dialog.CancelButton = cancel;
				return dialog.ShowDialog(this) == DialogResult.OK;
			}
		}

		private void RunBulkAction<T>(Func<T> action, Action<T> completed) {
			if(_isBulkActionRunning) return;
			_isBulkActionRunning = true;
			_lockAllButton.Enabled = false;
			_cleanupButton.Enabled = false;
			_updateDesktopRulesButton.Enabled = false;
			_manageDesktopRulesButton.Enabled = false;
			_applyDesktopRulesButton.Enabled = false;
			var thread = new Thread(() => {
				T result = default(T);
				Exception error = null;
				try { result = action(); } catch(Exception e) { error = e; }
				if(IsDisposed || !IsHandleCreated) return;
				try {
					BeginInvoke((Action)(() => {
						_isBulkActionRunning = false;
						_lockAllButton.Enabled = true;
						_cleanupButton.Enabled = true;
						_updateDesktopRulesButton.Enabled = true;
						_manageDesktopRulesButton.Enabled = true;
						_applyDesktopRulesButton.Enabled = true;
						if(error != null) {
							_statusLabel.Text = (Localizer.IsChinese ? "操作失败：" : "Operation failed: ") + error.Message;
							return;
						}
						completed(result);
					}));
				} catch(InvalidOperationException) { }
			}) { IsBackground = true };
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		private void WindowOverviewForm_KeyDown(object sender, KeyEventArgs e) {
			if(e.KeyCode == Keys.Escape) {
				e.Handled = true;
				e.SuppressKeyPress = true;
				Close();
				return;
			}
			if(e.KeyCode == Keys.Enter && _selectedItem != null) {
				e.Handled = true;
				e.SuppressKeyPress = true;
				ActivateSelectedWindow(true);
				return;
			}
			if(e.Control && e.KeyCode == Keys.L && _selectedItem != null) {
				e.Handled = true;
				e.SuppressKeyPress = true;
				var isProtected = App.Instance.ToggleOverviewWindowCleanupProtection(_selectedItem);
				_selectedItem.SetCleanupProtection(isProtected);
				_statusLabel.Text = Localizer.L(isProtected ? "Window locked for cleanup." : "Window unlocked for cleanup.");
				PopulateGrid();
				return;
			}
			if(e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) {
				if(MoveSelection(e.KeyCode)) {
					e.Handled = true;
					e.SuppressKeyPress = true;
				} else if(!_hasSnapshot) {
					_pendingNavigation.Add(e.KeyCode);
					e.Handled = true;
					e.SuppressKeyPress = true;
				}
			}
		}

		private void ApplyPendingNavigation() {
			if(_pendingNavigation.Count == 0) return;
			var pendingNavigation = _pendingNavigation.ToList();
			_pendingNavigation.Clear();
			foreach(var key in pendingNavigation) MoveSelection(key);
		}

		private bool MoveSelection(Keys key) {
			var currentList = _windowLists.FirstOrDefault(list => list.SelectedItems.Count > 0);
			if(_windowLists.Count == 0) return false;
			var listIndex = currentList == null ? FindNextListWithItems(-1, 1) : _windowLists.IndexOf(currentList);
			if(listIndex < 0) return false;
			var itemIndex = currentList == null ? 0 : currentList.SelectedIndices[0];
			if(key == Keys.Up || key == Keys.Down) {
				if(currentList == null) return SelectListItem(_windowLists[listIndex], 0);
				var verticalDirection = key == Keys.Up ? -1 : 1;
				var nextItemIndex = itemIndex + verticalDirection;
				if(nextItemIndex >= 0 && nextItemIndex < currentList.Items.Count) return SelectListItem(currentList, nextItemIndex);
				var verticalTargetListIndex = FindVerticalListWithItems(listIndex, verticalDirection);
				if(verticalTargetListIndex < 0) return false;
				var targetList = _windowLists[verticalTargetListIndex];
				return SelectListItem(targetList, verticalDirection > 0 ? 0 : targetList.Items.Count - 1);
			}
			var direction = key == Keys.Left ? -1 : 1;
			var targetListIndex = FindNextListWithItems(listIndex, direction);
			if(targetListIndex < 0) return false;
			return SelectListItem(_windowLists[targetListIndex], itemIndex);
		}

		private int FindNextListWithItems(int startIndex, int direction) {
			for(var index = startIndex + direction; index >= 0 && index < _windowLists.Count; index += direction) {
				if(_windowLists[index].Items.Count > 0) return index;
			}
			return -1;
		}

		private int FindVerticalListWithItems(int startIndex, int direction) {
			for(var index = startIndex + (direction * GridColumnCount); index >= 0 && index < _windowLists.Count; index += direction * GridColumnCount) {
				if(_windowLists[index].Items.Count > 0) return index;
			}
			return -1;
		}

		private bool SelectListItem(ListView list, int itemIndex) {
			if(list.Items.Count == 0) return false;
			itemIndex = Math.Max(0, Math.Min(itemIndex, list.Items.Count - 1));
			list.Focus();
			list.Items[itemIndex].Selected = true;
			list.EnsureVisible(itemIndex);
			return true;
		}

		private void ClearSearch() {
			_searchDebounceTimer.Stop();
			if(string.IsNullOrEmpty(_searchBox.Text)) return;
			_isClearingSearch = true;
			try {
				_searchBox.Clear();
			} finally {
				_isClearingSearch = false;
			}
			if(_hasSnapshot) PopulateGrid();
		}

		private void WindowOverviewForm_FormClosing(object sender, FormClosingEventArgs e) {
			if(e.CloseReason == CloseReason.UserClosing) {
				ClearSearch();
				_pendingNavigation.Clear();
				ActiveControl = _searchBox;
				_searchBox.Focus();
				_refreshVersion++;
				_isRefreshing = false;
				_refreshButton.Enabled = true;
				_loadingOverlay.Visible = false;
				_desktopGrid.Visible = true;
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
				var cardCount = Math.Max(1, DesktopCount);
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

		private sealed class BufferedTableLayoutPanel : TableLayoutPanel {
			private const int WM_SETREDRAW = 0x000B;

			[DllImport("user32.dll")]
			private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);

			internal BufferedTableLayoutPanel() {
				DoubleBuffered = true;
				ResizeRedraw = true;
			}

			internal void BeginGridUpdate() {
				SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
				SuspendLayout();
			}

			internal void EndGridUpdate() {
				ResumeLayout(true);
				SendMessage(Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
				Invalidate(true);
			}
		}
	}
}
