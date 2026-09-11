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
		[DataMember] public int Version = 1;
		[DataMember] public DateTime ExportedAtUtc;
		[DataMember] public string ApplicationVersion;
		[DataMember] public List<Settings.PersistedSetting> Settings = new List<Settings.PersistedSetting>();
		[DataMember] public List<string> DesktopNames = new List<string>();
		[DataMember] public List<ConfigurationBackupSnapshot> Snapshots = new List<ConfigurationBackupSnapshot>();
	}

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
	internal sealed class ConfigurationBackupDesktop {
		[DataMember] public int Index;
		[DataMember] public string Name;
	}

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

	internal sealed class ConfigurationBackupSummary {
		public DateTime ExportedAtUtc { get; internal set; }
		public string ApplicationVersion { get; internal set; }
		public int SettingsCount { get; internal set; }
		public int DesktopCount { get; internal set; }
		public int SnapshotCount { get; internal set; }
		internal ConfigurationBackupDocument Document { get; set; }
	}

	internal sealed class ConfigurationImportResult {
		public int SettingsCount { get; internal set; }
		public int SnapshotCount { get; internal set; }
		public int DesktopCount { get; internal set; }
		public List<string> DesktopErrors { get; } = new List<string>();
		public bool RequiresRestart { get; internal set; }
	}

	internal sealed class ConfigurationBackupService {
		private const int SupportedVersion = 1;
		private readonly App _app;

		internal ConfigurationBackupService(App app) { _app = app; }

		public void Export(string path) {
			if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A backup file path is required.");
			var document = new ConfigurationBackupDocument {
				ExportedAtUtc = DateTime.UtcNow,
				ApplicationVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
				Settings = Settings.ExportPersistedSettings(),
				DesktopNames = VirtualDesktopRegistry.GetDesktopNames(),
				Snapshots = ExportSnapshots(_app.DesktopLayoutSnapshots.List())
			};
			WriteDocument(path, document);
		}

		public ConfigurationBackupSummary ReadSummary(string path) {
			if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A backup file path is required.");
			try {
				using(var stream = File.OpenRead(path)) {
					var document = (ConfigurationBackupDocument)new DataContractJsonSerializer(typeof(ConfigurationBackupDocument)).ReadObject(stream);
					Validate(document);
					return new ConfigurationBackupSummary {
						Document = document,
						ExportedAtUtc = document.ExportedAtUtc,
						ApplicationVersion = document.ApplicationVersion,
						SettingsCount = document.Settings.Count,
						DesktopCount = document.DesktopNames.Count,
						SnapshotCount = document.Snapshots.Count
					};
				}
			} catch(Exception e) when(e is SerializationException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is ArgumentException) {
				throw new InvalidOperationException("Could not read the configuration backup. Local data was not changed.", e);
			}
		}

		public ConfigurationImportResult Import(ConfigurationBackupSummary summary) {
			if(summary == null || summary.Document == null) throw new ArgumentException("A validated configuration backup is required.");
			Validate(summary.Document);
			var oldSettings = Settings.ExportPersistedSettings();
			var oldSnapshots = _app.DesktopLayoutSnapshots.List();
			try {
				Settings.ReplacePersistedSettings(summary.Document.Settings);
				_app.DesktopLayoutSnapshots.ReplaceAll(ImportSnapshots(summary.Document.Snapshots));
			} catch {
				try { Settings.ReplacePersistedSettings(oldSettings); } catch { }
				try { _app.DesktopLayoutSnapshots.ReplaceAll(oldSnapshots); } catch { }
				throw new InvalidOperationException("Could not save the imported configuration. Existing settings and snapshots were restored.");
			}

			var result = new ConfigurationImportResult {
				SettingsCount = summary.SettingsCount,
				SnapshotCount = summary.SnapshotCount,
				DesktopCount = summary.DesktopCount,
				RequiresRestart = oldSettings.Any(item => item.Key == "general.startupWithWindows") || summary.Document.Settings.Any(item => item.Key == "general.startupWithWindows")
			};
			_app.RestoreDesktopConfiguration(summary.Document.DesktopNames, result.DesktopErrors);
			return result;
		}

		private static void Validate(ConfigurationBackupDocument document) {
			if(document == null || document.Version != SupportedVersion || string.IsNullOrWhiteSpace(document.ApplicationVersion) || document.Settings == null || document.DesktopNames == null || document.DesktopNames.Count == 0 || document.Snapshots == null) throw new InvalidDataException("The backup file has an unsupported or incomplete format.");
			if(document.ExportedAtUtc == default(DateTime)) throw new InvalidDataException("The backup file has no export time.");
			var keys = new HashSet<string>();
			foreach(var setting in document.Settings) {
				if(setting == null || string.IsNullOrWhiteSpace(setting.Key) || string.IsNullOrWhiteSpace(setting.Type) || setting.Value == null || !keys.Add(setting.Key)) throw new InvalidDataException("The backup contains invalid settings.");
			}
			Settings.ValidatePersistedSettings(document.Settings);
			foreach(var name in document.DesktopNames) if(name == null) throw new InvalidDataException("The backup contains an invalid desktop name.");
			new DesktopLayoutSnapshotRepository(Path.Combine(Path.GetTempPath(), "WindowsVirtualDesktopHelper-backup-validation.json")).ValidateForImport(ImportSnapshots(document.Snapshots));
		}

		private static void WriteDocument(string path, ConfigurationBackupDocument document) {
			var directory = Path.GetDirectoryName(Path.GetFullPath(path));
			Directory.CreateDirectory(directory);
			var temporaryPath = path + ".tmp";
			try {
				using(var stream = File.Create(temporaryPath)) new DataContractJsonSerializer(typeof(ConfigurationBackupDocument)).WriteObject(stream, document);
				if(File.Exists(path)) File.Replace(temporaryPath, path, null); else File.Move(temporaryPath, path);
			} catch {
				if(File.Exists(temporaryPath)) File.Delete(temporaryPath);
				throw;
			}
		}

		private static List<ConfigurationBackupSnapshot> ExportSnapshots(IEnumerable<DesktopLayoutSnapshot> snapshots) {
			var result = new List<ConfigurationBackupSnapshot>();
			foreach(var source in snapshots ?? Enumerable.Empty<DesktopLayoutSnapshot>()) {
				var snapshot = new ConfigurationBackupSnapshot { Id = source.Id, Name = source.Name, CreatedAtUtc = source.CreatedAtUtc, UpdatedAtUtc = source.UpdatedAtUtc };
				foreach(var desktop in source.Desktops) snapshot.Desktops.Add(new ConfigurationBackupDesktop { Index = desktop.Index, Name = desktop.Name });
				foreach(var window in source.Windows) snapshot.Windows.Add(new ConfigurationBackupWindow { DesktopIndex = window.DesktopIndex, ProcessPath = window.ProcessPath, ProcessName = window.ProcessName, ApplicationNameIsOverride = window.ApplicationNameIsOverride, AppUserModelId = window.AppUserModelId, WindowClassName = window.WindowClassName, WindowTitle = window.WindowTitle, WindowTitleIsRegex = window.WindowTitleIsRegex, ProcessStartTimeUtc = window.ProcessStartTimeUtc });
				result.Add(snapshot);
			}
			return result;
		}

		private static List<DesktopLayoutSnapshot> ImportSnapshots(IEnumerable<ConfigurationBackupSnapshot> snapshots) {
			if(snapshots == null) throw new InvalidDataException("The backup contains no snapshot collection.");
			var result = new List<DesktopLayoutSnapshot>();
			foreach(var source in snapshots) {
				if(source == null || source.Desktops == null || source.Windows == null) throw new InvalidDataException("The backup contains an incomplete snapshot.");
				var snapshot = new DesktopLayoutSnapshot { Id = source.Id, Name = source.Name, CreatedAtUtc = source.CreatedAtUtc, UpdatedAtUtc = source.UpdatedAtUtc };
				foreach(var desktop in source.Desktops) {
					if(desktop == null) throw new InvalidDataException("The backup contains an invalid snapshot desktop.");
					snapshot.Desktops.Add(new SnapshotDesktop { Index = desktop.Index, Name = desktop.Name });
				}
				foreach(var window in source.Windows) {
					if(window == null) throw new InvalidDataException("The backup contains an invalid snapshot window.");
					snapshot.Windows.Add(new SnapshotWindow { DesktopIndex = window.DesktopIndex, ProcessPath = window.ProcessPath, ProcessName = window.ProcessName, ApplicationNameIsOverride = window.ApplicationNameIsOverride, AppUserModelId = window.AppUserModelId, WindowClassName = window.WindowClassName, WindowTitle = window.WindowTitle, WindowTitleIsRegex = window.WindowTitleIsRegex, ProcessStartTimeUtc = window.ProcessStartTimeUtc });
				}
				result.Add(snapshot);
			}
			return result;
		}
	}

	internal static class ConfigurationBackupWorkflow {
		internal static string Export(App app, IWin32Window owner) {
			using(var dialog = new SaveFileDialog { Title = Localizer.L("Export Configuration Backup"), Filter = "Windows Virtual Desktop Helper Backup (*.wvdbak)|*.wvdbak|JSON Files (*.json)|*.json", DefaultExt = "wvdbak", AddExtension = true, FileName = "WindowsVirtualDesktopHelper-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".wvdbak" }) {
				if(dialog.ShowDialog(owner) != DialogResult.OK) return null;
				try {
					app.ConfigurationBackups.Export(dialog.FileName);
					return "Configuration backup exported to " + dialog.FileName + ".";
				} catch(Exception e) {
					ShowError(owner, "Could not export the configuration backup.", e);
					return null;
				}
			}
		}

		internal static string Import(App app, IWin32Window owner) {
			using(var dialog = new OpenFileDialog { Title = Localizer.L("Import Configuration Backup"), Filter = "Windows Virtual Desktop Helper Backup (*.wvdbak;*.json)|*.wvdbak;*.json|All Files (*.*)|*.*", CheckFileExists = true, Multiselect = false }) {
				if(dialog.ShowDialog(owner) != DialogResult.OK) return null;
				ConfigurationBackupSummary summary;
				try { summary = app.ConfigurationBackups.ReadSummary(dialog.FileName); }
				catch(Exception e) { ShowError(owner, "Could not read the configuration backup.", e); return null; }

				var message = "This replaces all saved application settings and desktop layout snapshots.\r\n\r\nExported: " + summary.ExportedAtUtc.ToLocalTime().ToString("g") + "\r\nApplication version: " + (summary.ApplicationVersion ?? "Unknown") + "\r\nSettings: " + summary.SettingsCount + "\r\nVirtual desktops: " + summary.DesktopCount + "\r\nSnapshots: " + summary.SnapshotCount + "\r\n\r\nMissing virtual desktops will be created and the first " + summary.DesktopCount + " desktop name" + (summary.DesktopCount == 1 ? "" : "s") + " restored. Extra local desktops and open windows are not changed.\r\n\r\nContinue?";
				if(MessageBox.Show(owner, message, "Import Configuration Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return null;
				try {
					var result = app.ConfigurationBackups.Import(summary);
					app.ApplyImportedConfiguration();
					if(app.DesktopLayoutSnapshotForm != null && !app.DesktopLayoutSnapshotForm.IsDisposed) app.DesktopLayoutSnapshotForm.RefreshSnapshots();
					var completion = "Imported " + result.SettingsCount + " settings, " + result.SnapshotCount + " snapshots, and restored " + result.DesktopCount + " desktop configuration entries.";
					if(result.DesktopErrors.Count > 0) completion += "\r\n\r\nDesktop configuration issues:\r\n" + string.Join("\r\n", result.DesktopErrors);
					if(result.RequiresRestart) completion += "\r\n\r\nStartup registration was not changed. Restart the application or confirm the Startup with Windows setting to reconcile it.";
					MessageBox.Show(owner, completion, "Import Configuration Backup", MessageBoxButtons.OK, result.DesktopErrors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
					return "Configuration backup imported.";
				} catch(Exception e) {
					ShowError(owner, "Could not import the configuration backup.", e);
					return null;
				}
			}
		}

		private static void ShowError(IWin32Window owner, string message, Exception error) {
			MessageBox.Show(owner, message + "\r\n\r\n" + error.Message, "Configuration Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}
}
