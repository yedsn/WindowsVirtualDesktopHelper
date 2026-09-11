using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper {
	internal static class Localizer {
		private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string> {
			{ "Error", "错误" }, { "Settings", "设置" }, { "Features", "功能" }, { "Language:", "语言：" },
			{ "Startup with Windows", "随 Windows 启动" }, { "Icon background:", "图标背景：" }, { "Icon text:", "图标文字：" }, { "Switch icon text:", "切换图标文字：" },
			{ "Export Backup", "导出备份" }, { "Import Backup", "导入备份" }, { "Tray type:", "托盘类型：" },
			{ "Previous / Next", "上一个 / 下一个" }, { "All Desktops", "所有桌面" }, { "Show Previous / Next Desktop in Icon Tray", "在托盘中显示上一个 / 下一个桌面" },
			{ "Show Desktop Name Initial in Icon Tray", "在托盘中显示桌面名称首字" }, { "Window manager:", "窗口管理器：" }, { "Built-in", "内置" }, { "System", "系统" },
			{ "Show Overlay when switching Desktop", "切换桌面时显示浮层" }, { "Animate In/Out", "显示 / 隐藏动画" }, { "Translucent", "半透明" },
			{ "Show on all Monitors", "在所有显示器上显示" }, { "Position:", "位置：" }, { "Show Permanent Overlay", "显示常驻浮层" }, { "Animate", "动画" },
			{ "Use Hot Keys to Jump to Desktop", "使用快捷键跳转到桌面" }, { "Use Alt+D to Open Window Manager", "使用 Alt+D 打开窗口管理器" },
			{ "Snapshot", "快照" }, { "New Snapshot", "新建快照" }, { "Restore Most Recent", "恢复最近快照" }, { "Manage Snapshots", "管理快照" },
			{ "Window Manager", "窗口管理器" }, { "Built-in Manager", "内置管理器" }, { "System Manager", "系统管理器" },
			{ "About", "关于" }, { "Donate", "捐赠" }, { "Exit", "退出" }, { "Previous Desktop", "上一个桌面" }, { "Next Desktop", "下一个桌面" },
			{ "Desktop Name", "桌面名称" }, { "Desktop Number", "桌面编号" }, { "Desktop Manager", "桌面管理器" },
			{ "All Windows", "所有窗口" }, { "Refresh", "刷新" }, { "Close Window", "关闭窗口" }, { "Activate Window", "激活窗口" }, { "Search windows", "搜索窗口" }, { "Other windows", "其他窗口" },
			{ "Lock All", "全部锁定" }, { "Manage Lock Rules", "管理锁定规则" }, { "Lock Rules", "锁定规则" }, { "New Lock Rule", "新增锁定规则" }, { "One-click Cleanup", "一键清理" }, { "One-click Cleanup ({0})", "一键清理（{0}）" }, { "Locked - protected from one-click cleanup", "已锁定 - 一键清理时保留" }, { "Unlocked - included in one-click cleanup", "未锁定 - 一键清理时会关闭" },
			{ "Review {0} windows across all desktops before cleanup.", "清理前请确认所有桌面上的 {0} 个窗口。" }, { "Confirm Cleanup", "确认清理" },
			{ "Window locked for cleanup.", "窗口已锁定，将在一键清理时保留。" }, { "Window unlocked for cleanup.", "窗口已解除锁定，将在一键清理时关闭。" },
			{ "Could not determine the current desktop.", "无法确定当前桌面。" }, { "Locked {0} windows on all desktops.", "已锁定所有桌面上的 {0} 个窗口。" },
			{ "No unprotected windows on any desktop.", "所有桌面都没有未锁定的窗口可清理。" }, { "Cleanup complete: {0} close requests sent; {1} could not be sent; {2} require administrator permission.", "清理完成：已发送 {0} 个关闭请求；{1} 个未能发送；其中 {2} 个需要管理员权限。" },
			{ "Administrator permission is required to close this window.", "关闭此窗口需要管理员权限。" },
			{ "Edit lock rules to protect matching windows from one-click cleanup. Rules match the process name and window title without case sensitivity. Enable regular expressions when the title changes.", "编辑锁定规则以保护匹配的窗口不被一键清理。规则会按程序名和窗口标题进行不区分大小写的匹配；窗口标题会变化时可启用正则表达式。" },
			{ "No lock rules saved.", "尚未保存锁定规则。" }, { "Could not save lock rules.", "无法保存锁定规则。" }, { "Remove this lock rule? Matching windows will no longer be protected.", "删除此锁定规则？匹配的窗口将不再受保护。" },
			{ "Desktop Layout Snapshots", "桌面布局快照" }, { "Name", "名称" }, { "Updated", "更新时间" }, { "Created", "创建时间" }, { "Desktops", "桌面" }, { "Windows", "窗口" },
			{ "Restore", "恢复" }, { "Update", "更新" }, { "Copy", "复制" }, { "View Details", "查看详情" }, { "Edit Window Rules", "编辑窗口规则" }, { "Rename", "重命名" }, { "Delete", "删除" },
			{ "Save", "保存" }, { "Cancel", "取消" }, { "Apply", "应用" }, { "Target desktop", "目标桌面" }, { "Window", "窗口" }, { "Application", "应用程序" }, { "Match type", "匹配方式" }, { "Exact", "精确匹配" }, { "Regular expression", "正则表达式" }, { "Use regular expression", "使用正则表达式" },
			{ "Status", "状态" }, { "Reason", "原因" }, { "Done", "完成" }, { "Preparing restore...", "正在准备恢复..." }, { "Restore Details", "恢复详情" },
			{ "Error Details", "错误详情" }, { "Open Issue on GitHub", "在 GitHub 提交问题" }, { "View Issues on GitHub", "查看 GitHub 问题" }, { "Issue Checklist", "问题检查清单" },
			{ "Show Log", "查看日志" }, { "Show Config", "查看配置" }, { "version ", "版本 " }, { "Donate via PayPal", "通过 PayPal 捐赠" }, { "by Dan Krusi", "作者：Dan Krusi" },
			{ "Log", "日志" }, { "Config", "配置" }, { "Configuration Backup", "配置备份" }, { "Import Configuration Backup", "导入配置备份" }, { "Export Configuration Backup", "导出配置备份" },
			{ "Unknown", "未知" }, { "Continue?", "是否继续？" }, { "English", "English" }, { "Simplified Chinese", "简体中文" }
			, { "No desktop layout snapshots saved.", "尚未保存桌面布局快照。" }, { "No desktop layout snapshots are available to restore.", "没有可恢复的桌面布局快照。" }
			, { "Could not capture the current layout.", "无法捕获当前布局。" }, { "Could not save the snapshot.", "无法保存快照。" }, { "Could not update the snapshot.", "无法更新快照。" }, { "Could not analyze the snapshot.", "无法分析快照。" }
			, { "New Desktop Layout Snapshot", "新建桌面布局快照" }, { "Update Desktop Layout Snapshot", "更新桌面布局快照" }, { "Save Snapshot", "保存快照" }, { "Update Snapshot", "更新快照" }
			, { "Copy Snapshot", "复制快照" }, { "Rename Snapshot", "重命名快照" }, { "Delete Snapshot", "删除快照" }, { "Delete Rule", "删除规则" }, { "Window Matching Rules", "窗口匹配规则" }
			, { "A snapshot name is required.", "必须输入快照名称。" }, { "Could not copy the snapshot.", "无法复制快照。" }, { "Could not save window matching rules.", "无法保存窗口匹配规则。" }, { "Could not rename the snapshot.", "无法重命名快照。" }, { "Could not delete the snapshot.", "无法删除快照。" }
			, { "A window name is required.", "必须输入窗口名称。" }, { "An application name is required.", "必须输入应用程序名称。" }, { "The title regular expression is invalid.", "窗口标题正则表达式无效。" }
			, { "Can restore", "可恢复" }, { "Already correct", "已在正确桌面" }, { "Not found", "未找到" }, { "Ambiguous match", "匹配不明确" }
			, { "WindowsVirtualDesktopHelper is already running.", "WindowsVirtualDesktopHelper 已在运行。" }
			, { "Window moved to Desktop ", "窗口已移动到桌面 " }
		};

		private static readonly Dictionary<string, string> English = CreateReverseMap();

		internal static bool IsChinese { get { return Settings.GetString("general.language", "en") == "zh-CN"; } }

		internal static string L(string text) {
			if(string.IsNullOrEmpty(text)) return text;
			string translated;
			return (IsChinese ? Chinese : English).TryGetValue(text, out translated) ? translated : text;
		}

		internal static string Desktop(int number) { return IsChinese ? "桌面 " + number : "Desktop " + number; }

		internal static void Apply(Control control) {
			if(control == null) return;
			control.Text = L(control.Text);
			foreach(Control child in control.Controls) Apply(child);
			var form = control as Form;
			if(form != null) Apply(form.MainMenuStrip);
		}

		internal static void Apply(ContextMenuStrip menu) {
			if(menu == null) return;
			foreach(ToolStripItem item in menu.Items) Apply(item);
		}

		internal static void Apply(MenuStrip menu) {
			if(menu == null) return;
			foreach(ToolStripItem item in menu.Items) Apply(item);
		}

		private static void Apply(ToolStripItem item) {
			item.Text = L(item.Text);
			var menu = item as ToolStripDropDownItem;
			if(menu == null) return;
			foreach(ToolStripItem child in menu.DropDownItems) Apply(child);
		}

		private static Dictionary<string, string> CreateReverseMap() {
			var result = new Dictionary<string, string>();
			foreach(var pair in Chinese) if(pair.Key != pair.Value) result[pair.Value] = pair.Key;
			return result;
		}
	}
}
