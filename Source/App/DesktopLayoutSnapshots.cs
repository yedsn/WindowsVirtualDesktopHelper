using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using WindowsVirtualDesktopHelper.Util;
using WindowsVirtualDesktopHelper.VirtualDesktopAPI;

namespace WindowsVirtualDesktopHelper {
	[DataContract]
	public sealed class DesktopLayoutSnapshotDocument {
		[DataMember] public int Version = 1;
		[DataMember] public List<DesktopLayoutSnapshot> Snapshots = new List<DesktopLayoutSnapshot>();
	}

	[DataContract]
	public sealed class DesktopLayoutSnapshot {
		[DataMember] public string Id;
		[DataMember] public string Name;
		[DataMember] public DateTime CreatedAtUtc;
		[DataMember] public DateTime UpdatedAtUtc;
		[DataMember] public List<SnapshotDesktop> Desktops = new List<SnapshotDesktop>();
		[DataMember] public List<SnapshotWindow> Windows = new List<SnapshotWindow>();
		public int DesktopCount { get { return Desktops == null ? 0 : Desktops.Count; } }
		public int WindowCount { get { return Windows == null ? 0 : Windows.Count; } }
	}

	[DataContract]
	public sealed class SnapshotDesktop {
		[DataMember] public int Index;
		[DataMember] public string Id;
		[DataMember] public string Name;
	}

	[DataContract]
	public sealed class SnapshotWindow {
		[DataMember] public int DesktopIndex;
		[DataMember] public string DesktopId;
		[DataMember] public string ProcessPath;
		[DataMember] public string ProcessName;
		[DataMember] public bool ApplicationNameIsOverride;
		[DataMember] public string AppUserModelId;
		[DataMember] public string WindowClassName;
		[DataMember] public string WindowTitle;
		[DataMember] public bool WindowTitleIsRegex;
		[DataMember(Name = "WindowTitleRegex", EmitDefaultValue = false)] public string LegacyWindowTitleRegex;
		[DataMember] public DateTime? ProcessStartTimeUtc;
		[DataMember] public int ProcessId;
		[DataMember] public long WindowHandle;
		public string DisplayName { get { return string.IsNullOrEmpty(WindowTitle) ? ProcessName : WindowTitle + " - " + ProcessName; } }
	}

	public enum SnapshotRestoreStatus { CanRestore, AlreadyCorrect, NotFound, Ambiguous, Moved, Failed }

	public sealed class SnapshotRestoreItem {
		public SnapshotWindow SavedWindow;
		public ApplicationWindow CurrentWindow;
		public int TargetDesktopIndex;
		public int CurrentDesktopIndex;
		public SnapshotRestoreStatus Status;
		public string Reason;
	}

	public sealed class SnapshotRestorePreview {
		public DesktopLayoutSnapshot Snapshot;
		public List<SnapshotRestoreItem> Items = new List<SnapshotRestoreItem>();
		public int Count(SnapshotRestoreStatus status) { return Items.Count(item => item.Status == status); }
	}

	public sealed class DesktopLayoutSnapshotRepository {
		private const int SupportedVersion = 1;
		private readonly string _path;

		public DesktopLayoutSnapshotRepository() {
			var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsVirtualDesktopHelper");
			_path = Path.Combine(directory, "desktop-layout-snapshots.json");
		}

		public DesktopLayoutSnapshotRepository(string path) {
			if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A snapshot file path is required.");
			_path = path;
		}

		public List<DesktopLayoutSnapshot> Load() {
			if(!File.Exists(_path)) return new List<DesktopLayoutSnapshot>();
			try {
				using(var stream = File.OpenRead(_path)) {
					var document = (DesktopLayoutSnapshotDocument)new DataContractJsonSerializer(typeof(DesktopLayoutSnapshotDocument)).ReadObject(stream);
					if(document == null || document.Version != SupportedVersion || document.Snapshots == null) throw new InvalidDataException("The snapshot file has an unsupported format.");
					foreach(var snapshot in document.Snapshots) { Normalize(snapshot); Validate(snapshot); }
					return document.Snapshots.OrderByDescending(snapshot => snapshot.UpdatedAtUtc).ToList();
				}
			} catch(Exception e) when(e is SerializationException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException) {
				throw new InvalidOperationException("Could not read desktop layout snapshots. The saved file was not changed.", e);
			}
		}

		public void Save(List<DesktopLayoutSnapshot> snapshots) {
			if(snapshots == null) throw new ArgumentNullException("snapshots");
			foreach(var snapshot in snapshots) { Normalize(snapshot); Validate(snapshot); }
			var directory = Path.GetDirectoryName(_path);
			Directory.CreateDirectory(directory);
			var temporaryPath = _path + ".tmp";
			try {
				using(var stream = File.Create(temporaryPath)) new DataContractJsonSerializer(typeof(DesktopLayoutSnapshotDocument)).WriteObject(stream, new DesktopLayoutSnapshotDocument { Snapshots = snapshots });
				if(File.Exists(_path)) File.Replace(temporaryPath, _path, null); else File.Move(temporaryPath, _path);
			} catch {
				if(File.Exists(temporaryPath)) File.Delete(temporaryPath);
				throw;
			}
		}

		private static void Validate(DesktopLayoutSnapshot snapshot) {
			if(snapshot == null || string.IsNullOrWhiteSpace(snapshot.Id) || string.IsNullOrWhiteSpace(snapshot.Name) || snapshot.Desktops == null || snapshot.Windows == null) throw new InvalidDataException("A desktop layout snapshot is incomplete.");
		}

		private static void Normalize(DesktopLayoutSnapshot snapshot) {
			if(snapshot == null || snapshot.Windows == null) return;
			foreach(var window in snapshot.Windows) {
				if(window == null || string.IsNullOrWhiteSpace(window.LegacyWindowTitleRegex)) continue;
				window.WindowTitle = window.LegacyWindowTitleRegex;
				window.WindowTitleIsRegex = true;
				window.LegacyWindowTitleRegex = null;
			}
		}
	}

	public sealed class DesktopLayoutSnapshotService {
		private sealed class CandidateMatch {
			public ApplicationWindow Window;
			public int DesktopIndex;
			public int Score;
		}

		private readonly App _app;
		private readonly DesktopLayoutSnapshotRepository _repository;

		internal DesktopLayoutSnapshotService(App app) { _app = app; _repository = new DesktopLayoutSnapshotRepository(); }
		public List<DesktopLayoutSnapshot> List() { return _repository.Load(); }

		public DesktopLayoutSnapshot Capture(string name) {
			if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required.");
			var desktopIndices = VirtualDesktopRegistry.GetDesktopIndices();
			var desktopNames = VirtualDesktopRegistry.GetDesktopNames();
			var snapshot = new DesktopLayoutSnapshot { Id = Guid.NewGuid().ToString("N"), Name = name.Trim(), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
			foreach(var desktop in desktopIndices.OrderBy(pair => pair.Value)) snapshot.Desktops.Add(new SnapshotDesktop { Index = desktop.Value, Id = desktop.Key.ToString(), Name = desktop.Value < desktopNames.Count ? desktopNames[desktop.Value] : null });
			using(var lookup = new WindowDesktopLookup()) foreach(var window in WindowEnumerator.GetApplicationWindows()) {
				Guid desktopId;
				int desktopIndex;
				if(!lookup.TryGetWindowDesktopId(window.Handle, out desktopId) || !desktopIndices.TryGetValue(desktopId, out desktopIndex)) continue;
				snapshot.Windows.Add(new SnapshotWindow { DesktopIndex = desktopIndex, DesktopId = desktopId.ToString(), ProcessPath = window.ProcessPath, ProcessName = window.ProcessName, AppUserModelId = window.AppUserModelId, WindowClassName = window.ClassName, WindowTitle = window.Title, ProcessStartTimeUtc = window.ProcessStartTimeUtc, ProcessId = window.ProcessId, WindowHandle = window.Handle.ToInt64() });
			}
			return snapshot;
		}

		public void Create(DesktopLayoutSnapshot snapshot) { var snapshots = List(); snapshots.Add(snapshot); _repository.Save(snapshots); }
		public DesktopLayoutSnapshot Copy(DesktopLayoutSnapshot source, string name) {
			if(source == null) throw new ArgumentNullException("source");
			if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required.");
			var copy = new DesktopLayoutSnapshot { Id = Guid.NewGuid().ToString("N"), Name = name.Trim(), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
			foreach(var desktop in source.Desktops) copy.Desktops.Add(new SnapshotDesktop { Index = desktop.Index, Id = desktop.Id, Name = desktop.Name });
			foreach(var window in source.Windows) copy.Windows.Add(new SnapshotWindow { DesktopIndex = window.DesktopIndex, DesktopId = window.DesktopId, ProcessPath = window.ProcessPath, ProcessName = window.ProcessName, ApplicationNameIsOverride = window.ApplicationNameIsOverride, AppUserModelId = window.AppUserModelId, WindowClassName = window.WindowClassName, WindowTitle = window.WindowTitle, WindowTitleIsRegex = window.WindowTitleIsRegex, ProcessStartTimeUtc = window.ProcessStartTimeUtc, ProcessId = window.ProcessId, WindowHandle = window.WindowHandle });
			Create(copy);
			return copy;
		}
		public void Update(DesktopLayoutSnapshot snapshot) { var snapshots = List(); var old = snapshots.FirstOrDefault(item => item.Id == snapshot.Id); if(old == null) throw new InvalidOperationException("The snapshot no longer exists."); snapshot.CreatedAtUtc = old.CreatedAtUtc; snapshot.UpdatedAtUtc = DateTime.UtcNow; snapshots[snapshots.IndexOf(old)] = snapshot; _repository.Save(snapshots); }
		public void Rename(string id, string name) { if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required."); var snapshots = List(); var snapshot = snapshots.FirstOrDefault(item => item.Id == id); if(snapshot == null) throw new InvalidOperationException("The snapshot no longer exists."); snapshot.Name = name.Trim(); snapshot.UpdatedAtUtc = DateTime.UtcNow; _repository.Save(snapshots); }
		public void Delete(string id) { var snapshots = List(); var snapshot = snapshots.FirstOrDefault(item => item.Id == id); if(snapshot == null) return; snapshots.Remove(snapshot); _repository.Save(snapshots); }

		public SnapshotRestorePreview Analyze(DesktopLayoutSnapshot snapshot) {
			if(snapshot == null) throw new ArgumentNullException("snapshot");
			var preview = new SnapshotRestorePreview { Snapshot = snapshot };
			var desktopIndices = VirtualDesktopRegistry.GetDesktopIndices();
			var current = new List<Tuple<ApplicationWindow, int>>();
			using(var lookup = new WindowDesktopLookup()) foreach(var window in WindowEnumerator.GetApplicationWindows()) {
				Guid desktopId; int desktopIndex;
				if(lookup.TryGetWindowDesktopId(window.Handle, out desktopId) && desktopIndices.TryGetValue(desktopId, out desktopIndex)) current.Add(Tuple.Create(window, desktopIndex));
			}
			var reservedHandles = new HashSet<IntPtr>();
			foreach(var saved in snapshot.Windows) {
				var target = ResolveExistingTarget(saved, snapshot, desktopIndices);
				var candidates = current.Where(item => StableIdentityMatches(saved, item.Item1) && !reservedHandles.Contains(item.Item1.Handle)).ToList();
				string matchError;
				var matches = SelectDiscriminatedMatches(saved, candidates, out matchError);
				var result = new SnapshotRestoreItem { SavedWindow = saved, TargetDesktopIndex = target };
				if(matches.Count == 0) { result.Status = SnapshotRestoreStatus.NotFound; result.Reason = matchError ?? "No reliably matching open window was found."; }
				else if(matches.Count > 1 && matches[0].Score == matches[1].Score) { result.Status = SnapshotRestoreStatus.Ambiguous; result.Reason = "More than one open window matches this saved window."; }
				else { result.CurrentWindow = matches[0].Window; result.CurrentDesktopIndex = matches[0].DesktopIndex; reservedHandles.Add(result.CurrentWindow.Handle); if(matches[0].DesktopIndex == target) { result.Status = SnapshotRestoreStatus.AlreadyCorrect; result.Reason = "The window is already on its target desktop."; } else { result.Status = SnapshotRestoreStatus.CanRestore; result.Reason = "The window can be moved to its target desktop."; } }
				preview.Items.Add(result);
			}
			return preview;
		}

		public List<SnapshotRestoreItem> Restore(SnapshotRestorePreview preview, Action<int, int, SnapshotRestoreItem> progress) {
			if(preview == null) throw new ArgumentNullException("preview");
			var movable = preview.Items.Where(item => item.Status == SnapshotRestoreStatus.CanRestore).ToList();
			var required = Math.Max(preview.Snapshot.Desktops.Count == 0 ? 0 : preview.Snapshot.Desktops.Max(desktop => desktop.Index), preview.Snapshot.Windows.Count == 0 ? 0 : preview.Snapshot.Windows.Max(window => window.DesktopIndex));
			_app.EnsureDesktopCount(required + 1);
			for(var i = 0; i < movable.Count; i++) {
				var item = movable[i];
				progress?.Invoke(i + 1, movable.Count, item);
				if(!WindowEnumerator.IsWindow(item.CurrentWindow.Handle)) { item.Status = SnapshotRestoreStatus.Failed; item.Reason = "The window closed before it could be moved."; continue; }
				string error;
				if(_app.TryMoveWindowToDesktop(item.CurrentWindow.Handle, item.TargetDesktopIndex, out error)) { item.Status = SnapshotRestoreStatus.Moved; item.Reason = "Moved to Desktop " + (item.TargetDesktopIndex + 1) + "."; }
				else { item.Status = SnapshotRestoreStatus.Failed; item.Reason = error; }
			}
			return preview.Items;
		}

		private static int ResolveExistingTarget(SnapshotWindow saved, DesktopLayoutSnapshot snapshot, Dictionary<Guid, int> desktopIndices) {
			Guid savedDesktopId;
			return Guid.TryParse(saved.DesktopId, out savedDesktopId) && desktopIndices.ContainsKey(savedDesktopId) ? desktopIndices[savedDesktopId] : saved.DesktopIndex;
		}

		private static int Score(SnapshotWindow saved, ApplicationWindow candidate) {
			var score = 0;
			if(saved.ApplicationNameIsOverride && !string.IsNullOrEmpty(saved.ProcessName) && Same(saved.ProcessName, candidate.ProcessName)) score += 100;
			else if(!string.IsNullOrEmpty(saved.ProcessPath) && Same(saved.ProcessPath, candidate.ProcessPath)) score += 400;
			else if(!string.IsNullOrEmpty(saved.AppUserModelId) && Same(saved.AppUserModelId, candidate.AppUserModelId)) score += 300;
			else if(!string.IsNullOrEmpty(saved.ProcessName) && Same(saved.ProcessName, candidate.ProcessName) && !string.IsNullOrEmpty(saved.WindowClassName) && Same(saved.WindowClassName, candidate.ClassName)) score += 100;
			if(saved.WindowTitleIsRegex || (!string.IsNullOrEmpty(saved.WindowTitle) && Same(saved.WindowTitle, candidate.Title))) score += 40;
			if(saved.ProcessStartTimeUtc.HasValue && candidate.ProcessStartTimeUtc.HasValue && saved.ProcessStartTimeUtc.Value == candidate.ProcessStartTimeUtc.Value) score += 20;
			return score;
		}

		private static List<CandidateMatch> SelectDiscriminatedMatches(SnapshotWindow saved, List<Tuple<ApplicationWindow, int>> candidates, out string error) {
			error = null;
			var scored = candidates.Select(item => new CandidateMatch { Window = item.Item1, DesktopIndex = item.Item2, Score = Score(saved, item.Item1) }).ToList();
			if(saved.WindowTitleIsRegex) {
				try {
					return scored.Where(item => Regex.IsMatch(item.Window.Title ?? "", saved.WindowTitle ?? "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))).OrderByDescending(item => item.Score).ToList();
				} catch(ArgumentException) {
					error = "The saved window-title regular expression is invalid.";
					return new List<CandidateMatch>();
				} catch(RegexMatchTimeoutException) {
					error = "The saved window-title regular expression took too long to evaluate.";
					return new List<CandidateMatch>();
				}
			}
			if(!string.IsNullOrEmpty(saved.WindowTitle)) {
				var exactTitle = scored.Where(item => Same(saved.WindowTitle, item.Window.Title)).ToList();
				if(exactTitle.Count > 0) return exactTitle.OrderByDescending(item => item.Score).ToList();
				if(saved.ProcessStartTimeUtc.HasValue) {
					var exactStart = scored.Where(item => item.Window.ProcessStartTimeUtc.HasValue && item.Window.ProcessStartTimeUtc.Value == saved.ProcessStartTimeUtc.Value).ToList();
					if(exactStart.Count > 0) return exactStart.OrderByDescending(item => item.Score).ToList();
				}
				return new List<CandidateMatch>();
			}
			if(saved.ProcessStartTimeUtc.HasValue) {
				var exactStart = scored.Where(item => item.Window.ProcessStartTimeUtc.HasValue && item.Window.ProcessStartTimeUtc.Value == saved.ProcessStartTimeUtc.Value).ToList();
				if(exactStart.Count > 0) return exactStart.OrderByDescending(item => item.Score).ToList();
			}
			return scored.OrderByDescending(item => item.Score).ToList();
		}

		private static bool StableIdentityMatches(SnapshotWindow saved, ApplicationWindow candidate) {
			if(saved.ApplicationNameIsOverride) return !string.IsNullOrEmpty(saved.ProcessName) && Same(saved.ProcessName, candidate.ProcessName);
			if(!string.IsNullOrEmpty(saved.ProcessPath) && Same(saved.ProcessPath, candidate.ProcessPath)) return true;
			if(!string.IsNullOrEmpty(saved.AppUserModelId) && Same(saved.AppUserModelId, candidate.AppUserModelId)) return true;
			return !string.IsNullOrEmpty(saved.ProcessName) && Same(saved.ProcessName, candidate.ProcessName)
				&& !string.IsNullOrEmpty(saved.WindowClassName) && Same(saved.WindowClassName, candidate.ClassName);
		}

		private static bool Same(string left, string right) { return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
	}
}
