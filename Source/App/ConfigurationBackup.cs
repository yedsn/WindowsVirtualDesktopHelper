using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using WindowsVirtualDesktopHelper.VirtualDesktopAPI;

namespace WindowsVirtualDesktopHelper {
	[DataContract]
	internal sealed class ConfigurationBackupDocument {
		[DataMember] public int Version = 2;
		[DataMember] public DateTime ExportedAtUtc;
		[DataMember] public string ApplicationVersion;
		[DataMember] public List<Settings.PersistedSetting> Settings = new List<Settings.PersistedSetting>();
		[DataMember] public List<string> DesktopNames = new List<string>();
		[DataMember(EmitDefaultValue = false)] public DesktopRuleStateDocument RuleState;
		#pragma warning disable CS0649 // Only populated by DataContractJsonSerializer for version 1 imports.
		[DataMember(EmitDefaultValue = false)] public List<ConfigurationBackupSnapshot> Snapshots;
		#pragma warning restore CS0649
	}

	#pragma warning disable CS0649
	[DataContract]
	internal sealed class ConfigurationBackupSnapshot {
		[DataMember] public string Id;
		[DataMember] public string Name;
		[DataMember] public DateTime CreatedAtUtc;
		[DataMember] public DateTime UpdatedAtUtc;
		[DataMember] public List<ConfigurationBackupDesktop> Desktops = new List<ConfigurationBackupDesktop>();
		[DataMember] public List<ConfigurationBackupWindow> Windows = new List<ConfigurationBackupWindow>();
	}
	[DataContract]
	internal sealed class ConfigurationBackupDesktop { [DataMember] public int Index; [DataMember] public string Name; }
	[DataContract]
	internal sealed class ConfigurationBackupWindow {
		[DataMember] public int DesktopIndex;
		[DataMember] public string ProcessPath;
		[DataMember] public string ProcessName;
		[DataMember] public bool ApplicationNameIsOverride;
		[DataMember] public string AppUserModelId;
		[DataMember] public string WindowClassName;
		[DataMember] public string WindowTitle;
		[DataMember] public bool WindowTitleIsRegex;
		[DataMember] public DateTime? ProcessStartTimeUtc;
	}
	#pragma warning restore CS0649

	internal sealed class ConfigurationBackupSummary {
		public DateTime ExportedAtUtc { get; internal set; }
		public string ApplicationVersion { get; internal set; }
		public int SettingsCount { get; internal set; }
		public int DesktopCount { get; internal set; }
		public int SnapshotCount { get; internal set; }
		public int CleanupRuleCount { get; internal set; }
		public int DesktopRuleCount { get; internal set; }
		public bool IsLegacyRuleState { get; internal set; }
		internal ConfigurationBackupDocument Document { get; set; }
	}

	internal sealed class ConfigurationImportResult {
		public int SettingsCount { get; internal set; }
		public int SnapshotCount { get; internal set; }
		public int DesktopCount { get; internal set; }
		public bool ImportedLegacyRuleState { get; internal set; }
		public List<string> DesktopErrors { get; } = new List<string>();
		public bool RequiresRestart { get; internal set; }
	}

	internal sealed class ConfigurationBackupService {
		private const int CurrentVersion = 2;
		private readonly App _app;
		internal ConfigurationBackupService(App app) { _app = app; }
		public void Export(string path) {
			if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A backup file path is required.");
			WriteDocument(path, new ConfigurationBackupDocument { ExportedAtUtc = DateTime.UtcNow, ApplicationVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(), Settings = Settings.ExportPersistedSettings(), DesktopNames = VirtualDesktopRegistry.GetDesktopNames(), RuleState = ExportRuleState(_app.DesktopRuleState.Export()) });
		}
		public ConfigurationBackupSummary ReadSummary(string path) {
			if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A backup file path is required.");
			try {
				using(var stream = File.OpenRead(path)) {
					var document = (ConfigurationBackupDocument)new DataContractJsonSerializer(typeof(ConfigurationBackupDocument)).ReadObject(stream);
					var state = ValidateAndGetRuleState(document, null, out var legacy);
					return new ConfigurationBackupSummary { Document = document, ExportedAtUtc = document.ExportedAtUtc, ApplicationVersion = document.ApplicationVersion, SettingsCount = document.Settings.Count, DesktopCount = document.DesktopNames.Count, SnapshotCount = state.Snapshots.Count, CleanupRuleCount = state.CleanupRules.Count, DesktopRuleCount = state.DesktopRules.Count, IsLegacyRuleState = legacy };
				}
			} catch(Exception e) when(e is SerializationException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { throw new InvalidOperationException("Could not read the configuration backup. Local data was not changed.", e); }
		}
		public ConfigurationImportResult Import(ConfigurationBackupSummary summary) {
			if(summary == null || summary.Document == null) throw new ArgumentException("A validated configuration backup is required.");
			var oldSettings = Settings.ExportPersistedSettings(); var oldState = _app.DesktopRuleState.Export();
			var state = ValidateAndGetRuleState(summary.Document, oldState.CleanupRules, out var legacy);
			try { Settings.ReplacePersistedSettings(summary.Document.Settings); _app.DesktopRuleState.Replace(state); }
			catch { try { Settings.ReplacePersistedSettings(oldSettings); } catch { } try { _app.DesktopRuleState.Replace(oldState); } catch { } throw new InvalidOperationException("Could not save the imported configuration. Existing settings and rules were restored."); }
			var result = new ConfigurationImportResult { SettingsCount = summary.SettingsCount, SnapshotCount = state.Snapshots.Count, DesktopCount = summary.DesktopCount, ImportedLegacyRuleState = legacy, RequiresRestart = oldSettings.Any(item => item.Key == "general.startupWithWindows") || summary.Document.Settings.Any(item => item.Key == "general.startupWithWindows") };
			_app.RestoreDesktopConfiguration(summary.Document.DesktopNames, result.DesktopErrors);
			return result;
		}
		private static DesktopRuleStateDocument ValidateAndGetRuleState(ConfigurationBackupDocument document, List<WindowCleanupLockRule> existingCleanupRules, out bool legacy) {
			if(document == null || (document.Version != 1 && document.Version != CurrentVersion) || string.IsNullOrWhiteSpace(document.ApplicationVersion) || document.Settings == null || document.DesktopNames == null || document.DesktopNames.Count == 0) throw new InvalidDataException("The backup file has an unsupported or incomplete format.");
			if(document.ExportedAtUtc == default(DateTime)) throw new InvalidDataException("The backup file has no export time.");
			var keys = new HashSet<string>(); foreach(var setting in document.Settings) if(setting == null || string.IsNullOrWhiteSpace(setting.Key) || string.IsNullOrWhiteSpace(setting.Type) || setting.Value == null || !keys.Add(setting.Key)) throw new InvalidDataException("The backup contains invalid settings.");
			Settings.ValidatePersistedSettings(document.Settings); foreach(var name in document.DesktopNames) if(name == null) throw new InvalidDataException("The backup contains an invalid desktop name.");
			legacy = document.Version == 1 || document.RuleState == null;
			var state = legacy ? ImportLegacySnapshots(document.Snapshots, existingCleanupRules) : document.RuleState.Copy();
			DesktopRuleStateRepository.Validate(state);
			return state;
		}
		private static DesktopRuleStateDocument ImportLegacySnapshots(IEnumerable<ConfigurationBackupSnapshot> snapshots, IEnumerable<WindowCleanupLockRule> existingCleanupRules) {
			if(snapshots == null) throw new InvalidDataException("The backup contains no snapshot collection.");
			var state = new DesktopRuleStateDocument { CleanupRules = (existingCleanupRules ?? Enumerable.Empty<WindowCleanupLockRule>()).Select(rule => rule.Copy()).ToList() };
			foreach(var source in snapshots) {
				if(source == null || source.Windows == null) throw new InvalidDataException("The backup contains an incomplete snapshot.");
				state.Snapshots.Add(new RuleSnapshot { Id = source.Id, Name = source.Name, CreatedAtUtc = source.UpdatedAtUtc == default(DateTime) ? source.CreatedAtUtc : source.UpdatedAtUtc, Kind = RuleSnapshotKind.Legacy, CleanupRulesAvailable = false, CleanupRules = new List<WindowCleanupLockRule>(), DesktopRules = source.Windows.Select(window => { if(window == null) throw new InvalidDataException("The backup contains an invalid snapshot window."); return new DesktopRule { DesktopIndex = window.DesktopIndex, ProcessPath = window.ProcessPath, ProcessName = window.ProcessName, ApplicationNameIsOverride = window.ApplicationNameIsOverride, AppUserModelId = window.AppUserModelId, WindowClassName = window.WindowClassName, WindowTitle = window.WindowTitle, WindowTitleIsRegex = window.WindowTitleIsRegex, ProcessStartTimeUtc = window.ProcessStartTimeUtc }; }).ToList() });
			}
			return state;
		}
		private static DesktopRuleStateDocument ExportRuleState(DesktopRuleStateDocument state) {
			var portable = state.Copy();
			foreach(var rule in portable.DesktopRules) rule.DesktopId = null;
			foreach(var snapshot in portable.Snapshots) foreach(var rule in snapshot.DesktopRules) rule.DesktopId = null;
			return portable;
		}
		private static void WriteDocument(string path, ConfigurationBackupDocument document) {
			var directory = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(directory); var temporaryPath = path + ".tmp";
			try { using(var stream = File.Create(temporaryPath)) new DataContractJsonSerializer(typeof(ConfigurationBackupDocument)).WriteObject(stream, document); if(File.Exists(path)) File.Replace(temporaryPath, path, null); else File.Move(temporaryPath, path); }
			catch { if(File.Exists(temporaryPath)) File.Delete(temporaryPath); throw; }
		}
	}

	internal static class ConfigurationBackupWorkflow {
		internal static string Export(App app, IWin32Window owner) {
			using(var dialog = new SaveFileDialog { Title = Localizer.L("Export Configuration Backup"), Filter = "Windows Virtual Desktop Helper Backup (*.wvdbak)|*.wvdbak|JSON Files (*.json)|*.json", DefaultExt = "wvdbak", AddExtension = true, FileName = "WindowsVirtualDesktopHelper-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".wvdbak" }) {
				if(dialog.ShowDialog(owner) != DialogResult.OK) return null;
				try { app.ConfigurationBackups.Export(dialog.FileName); return Localizer.L("Configuration backup exported."); } catch(Exception e) { ShowError(owner, Localizer.L("Could not export the configuration backup."), e); return null; }
			}
		}
		internal static string Import(App app, IWin32Window owner) {
			using(var dialog = new OpenFileDialog { Title = Localizer.L("Import Configuration Backup"), Filter = "Windows Virtual Desktop Helper Backup (*.wvdbak;*.json)|*.wvdbak;*.json|All Files (*.*)|*.*", CheckFileExists = true, Multiselect = false }) {
				if(dialog.ShowDialog(owner) != DialogResult.OK) return null;
				ConfigurationBackupSummary summary; try { summary = app.ConfigurationBackups.ReadSummary(dialog.FileName); } catch(Exception e) { ShowError(owner, Localizer.L("Could not read the configuration backup."), e); return null; }
				var message = Localizer.L("This replaces saved settings, cleanup rules, desktop rules, and rule snapshots. Open windows will not be moved.") + "\r\n\r\n" + Localizer.L("Exported") + ": " + summary.ExportedAtUtc.ToLocalTime().ToString("g") + "\r\n" + Localizer.L("Settings") + ": " + summary.SettingsCount + "\r\n" + Localizer.L("Virtual desktops") + ": " + summary.DesktopCount + "\r\n" + Localizer.L("Cleanup rules") + ": " + summary.CleanupRuleCount + "\r\n" + Localizer.L("Desktop rules") + ": " + summary.DesktopRuleCount + "\r\n" + Localizer.L("Snapshots") + ": " + summary.SnapshotCount + "\r\n\r\n" + Localizer.L("Continue?");
				if(summary.IsLegacyRuleState) message += "\r\n\r\n" + Localizer.L("This older backup has no cleanup-rule history. Current cleanup rules will be retained.");
				if(MessageBox.Show(owner, message, Localizer.L("Import Configuration Backup"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return null;
				try { var result = app.ConfigurationBackups.Import(summary); app.ApplyImportedConfiguration(); if(app.DesktopLayoutSnapshotForm != null && !app.DesktopLayoutSnapshotForm.IsDisposed) app.DesktopLayoutSnapshotForm.RefreshSnapshots(); var completion = Localizer.L("Configuration backup imported."); if(result.ImportedLegacyRuleState) completion += "\r\n\r\n" + Localizer.L("Legacy snapshots were imported. Current cleanup rules were retained."); if(result.DesktopErrors.Count > 0) completion += "\r\n\r\n" + string.Join("\r\n", result.DesktopErrors); MessageBox.Show(owner, completion, Localizer.L("Configuration Backup"), MessageBoxButtons.OK, result.DesktopErrors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning); return completion; } catch(Exception e) { ShowError(owner, Localizer.L("Could not import the configuration backup."), e); return null; }
			}
		}
		private static void ShowError(IWin32Window owner, string message, Exception error) { MessageBox.Show(owner, message + "\r\n\r\n" + error.Message, Localizer.L("Configuration Backup"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
	}
}
