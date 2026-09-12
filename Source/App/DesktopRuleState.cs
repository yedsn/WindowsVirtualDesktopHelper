using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using WindowsVirtualDesktopHelper.Util;
using WindowsVirtualDesktopHelper.VirtualDesktopAPI;

namespace WindowsVirtualDesktopHelper {
	public enum RuleSnapshotKind { Manual, Automatic, Legacy }

	[DataContract]
	public sealed class DesktopRule {
		[DataMember] public int DesktopIndex;
		[DataMember(EmitDefaultValue = false)] public string DesktopId;
		[DataMember] public string ProcessPath;
		[DataMember] public string ProcessName;
		[DataMember] public bool ApplicationNameIsOverride;
		[DataMember] public string AppUserModelId;
		[DataMember] public string WindowClassName;
		[DataMember] public string WindowTitle;
		[DataMember] public bool WindowTitleIsRegex;
		[DataMember] public DateTime? ProcessStartTimeUtc;
		public string DisplayName { get { return string.IsNullOrEmpty(WindowTitle) ? ProcessName : WindowTitle + " - " + ProcessName; } }

		public DesktopRule Copy() {
			return new DesktopRule { DesktopIndex = DesktopIndex, DesktopId = DesktopId, ProcessPath = ProcessPath, ProcessName = ProcessName, ApplicationNameIsOverride = ApplicationNameIsOverride, AppUserModelId = AppUserModelId, WindowClassName = WindowClassName, WindowTitle = WindowTitle, WindowTitleIsRegex = WindowTitleIsRegex, ProcessStartTimeUtc = ProcessStartTimeUtc };
		}
	}

	[DataContract]
	public sealed class RuleSnapshot {
		[DataMember] public string Id;
		[DataMember] public string Name;
		[DataMember] public DateTime CreatedAtUtc;
		[DataMember] public RuleSnapshotKind Kind;
		[DataMember] public bool CleanupRulesAvailable = true;
		[DataMember] public List<WindowCleanupLockRule> CleanupRules = new List<WindowCleanupLockRule>();
		[DataMember] public List<DesktopRule> DesktopRules = new List<DesktopRule>();
		public int CleanupRuleCount { get { return CleanupRules == null ? 0 : CleanupRules.Count; } }
		public int DesktopRuleCount { get { return DesktopRules == null ? 0 : DesktopRules.Count; } }
		public RuleSnapshot Copy() {
			return new RuleSnapshot { Id = Id, Name = Name, CreatedAtUtc = CreatedAtUtc, Kind = Kind, CleanupRulesAvailable = CleanupRulesAvailable, CleanupRules = (CleanupRules ?? new List<WindowCleanupLockRule>()).Select(rule => rule.Copy()).ToList(), DesktopRules = (DesktopRules ?? new List<DesktopRule>()).Select(rule => rule.Copy()).ToList() };
		}
	}

	[DataContract]
	public sealed class DesktopRuleStateDocument {
		[DataMember] public int Version = 1;
		[DataMember] public List<WindowCleanupLockRule> CleanupRules = new List<WindowCleanupLockRule>();
		[DataMember] public List<DesktopRule> DesktopRules = new List<DesktopRule>();
		[DataMember] public List<RuleSnapshot> Snapshots = new List<RuleSnapshot>();
		public DesktopRuleStateDocument Copy() {
			return new DesktopRuleStateDocument { CleanupRules = (CleanupRules ?? new List<WindowCleanupLockRule>()).Select(rule => rule.Copy()).ToList(), DesktopRules = (DesktopRules ?? new List<DesktopRule>()).Select(rule => rule.Copy()).ToList(), Snapshots = (Snapshots ?? new List<RuleSnapshot>()).Select(snapshot => snapshot.Copy()).ToList() };
		}
	}

	public sealed class DesktopRuleStateRepository {
		private const int SupportedVersion = 1;
		private readonly string _path;
		public DesktopRuleStateRepository() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsVirtualDesktopHelper", "desktop-rule-state.json")) { }
		public DesktopRuleStateRepository(string path) { _path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("A rule-state file path is required.") : path; }
		public bool Exists { get { return File.Exists(_path); } }
		public DesktopRuleStateDocument Load() {
			try {
				using(var stream = File.OpenRead(_path)) {
					var document = (DesktopRuleStateDocument)new DataContractJsonSerializer(typeof(DesktopRuleStateDocument)).ReadObject(stream);
					Validate(document);
					return document;
				}
			} catch(Exception e) when(e is SerializationException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException) {
				throw new InvalidOperationException("Could not read desktop rules. The saved file was not changed.", e);
			}
		}
		public void Save(DesktopRuleStateDocument document) {
			Validate(document);
			Directory.CreateDirectory(Path.GetDirectoryName(_path));
			var temporaryPath = _path + ".tmp";
			try {
				using(var stream = File.Create(temporaryPath)) new DataContractJsonSerializer(typeof(DesktopRuleStateDocument)).WriteObject(stream, document);
				if(File.Exists(_path)) File.Replace(temporaryPath, _path, null); else File.Move(temporaryPath, _path);
			} catch {
				if(File.Exists(temporaryPath)) File.Delete(temporaryPath);
				throw;
			}
		}
		public static void Validate(DesktopRuleStateDocument document) {
			if(document == null || document.Version != SupportedVersion || document.CleanupRules == null || document.DesktopRules == null || document.Snapshots == null) throw new InvalidDataException("The desktop rule state has an unsupported or incomplete format.");
			ValidateCleanupRules(document.CleanupRules);
			ValidateDesktopRules(document.DesktopRules);
			foreach(var snapshot in document.Snapshots) {
				if(snapshot == null || string.IsNullOrWhiteSpace(snapshot.Id) || string.IsNullOrWhiteSpace(snapshot.Name) || snapshot.CreatedAtUtc == default(DateTime) || snapshot.CleanupRules == null || snapshot.DesktopRules == null) throw new InvalidDataException("A rule snapshot is incomplete.");
				if(snapshot.CleanupRulesAvailable) ValidateCleanupRules(snapshot.CleanupRules);
				ValidateDesktopRules(snapshot.DesktopRules);
			}
		}
		internal static void ValidateCleanupRules(IEnumerable<WindowCleanupLockRule> rules) {
			if(rules.Any(rule => rule == null || string.IsNullOrWhiteSpace(rule.ProcessName))) throw new InvalidDataException("A cleanup lock rule is incomplete.");
			foreach(var rule in rules.Where(rule => rule.WindowTitleIsRegex)) ValidateRegex(rule.WindowTitle, "A cleanup lock title regular expression is invalid.");
		}
		internal static void ValidateDesktopRules(IEnumerable<DesktopRule> rules) {
			if(rules.Any(rule => rule == null || rule.DesktopIndex < 0 || string.IsNullOrWhiteSpace(rule.ProcessName) || string.IsNullOrWhiteSpace(rule.WindowTitle))) throw new InvalidDataException("A desktop rule is incomplete.");
			foreach(var rule in rules.Where(rule => rule.WindowTitleIsRegex)) ValidateRegex(rule.WindowTitle, "A desktop rule title regular expression is invalid.");
		}
		private static void ValidateRegex(string expression, string message) {
			try { new Regex(expression ?? "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)); }
			catch(ArgumentException e) { throw new InvalidDataException(message, e); }
		}
	}

	public sealed class DesktopRuleStateService {
		private readonly object _sync = new object();
		private readonly DesktopRuleStateRepository _repository;
		private DesktopRuleStateDocument _state;
		public DesktopRuleStateService() : this(new DesktopRuleStateRepository()) { }
		internal DesktopRuleStateService(DesktopRuleStateRepository repository) {
			_repository = repository;
			_state = _repository.Exists ? _repository.Load() : MigrateLegacyState();
			if(!_repository.Exists) _repository.Save(_state);
		}
		public DesktopRuleStateDocument Export() { lock(_sync) return _state.Copy(); }
		public void Replace(DesktopRuleStateDocument replacement) { lock(_sync) Save(replacement.Copy()); }
		public List<WindowCleanupLockRule> ListCleanupRules() { lock(_sync) return _state.CleanupRules.Select(rule => rule.Copy()).ToList(); }
		public void ReplaceCleanupRules(IEnumerable<WindowCleanupLockRule> rules) { lock(_sync) { var state = _state.Copy(); state.CleanupRules = (rules ?? throw new ArgumentNullException("rules")).Select(rule => rule == null ? null : rule.Copy()).ToList(); Save(state); } }
		public List<DesktopRule> ListDesktopRules() { lock(_sync) return _state.DesktopRules.Select(rule => rule.Copy()).ToList(); }
		public void ReplaceDesktopRules(IEnumerable<DesktopRule> rules) { lock(_sync) { var state = _state.Copy(); state.DesktopRules = (rules ?? throw new ArgumentNullException("rules")).Select(rule => rule == null ? null : rule.Copy()).ToList(); Save(state); } }
		public List<RuleSnapshot> ListSnapshots() { lock(_sync) return _state.Snapshots.Select(snapshot => snapshot.Copy()).OrderByDescending(snapshot => snapshot.CreatedAtUtc).ToList(); }
		public RuleSnapshot GetSnapshot(string id) { lock(_sync) { var snapshot = _state.Snapshots.FirstOrDefault(item => item.Id == id); return snapshot == null ? null : snapshot.Copy(); } }
		public RuleSnapshot CreateSnapshot(string name, RuleSnapshotKind kind) {
			if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required.");
			lock(_sync) {
				var state = _state.Copy();
				var snapshot = new RuleSnapshot { Id = Guid.NewGuid().ToString("N"), Name = name.Trim(), CreatedAtUtc = DateTime.UtcNow, Kind = kind, CleanupRulesAvailable = true, CleanupRules = state.CleanupRules.Select(rule => rule.Copy()).ToList(), DesktopRules = state.DesktopRules.Select(rule => rule.Copy()).ToList() };
				state.Snapshots.Add(snapshot);
				if(kind == RuleSnapshotKind.Automatic) PruneAutomaticSnapshots(state);
				Save(state);
				return snapshot.Copy();
			}
		}
		public void RestoreSnapshot(string id) {
			lock(_sync) {
				var state = _state.Copy();
				var snapshot = state.Snapshots.FirstOrDefault(item => item.Id == id);
				if(snapshot == null) throw new InvalidOperationException("The snapshot no longer exists.");
				if(snapshot.CleanupRulesAvailable) state.CleanupRules = snapshot.CleanupRules.Select(rule => rule.Copy()).ToList();
				state.DesktopRules = snapshot.DesktopRules.Select(rule => rule.Copy()).ToList();
				Save(state);
			}
		}
		public void RenameSnapshot(string id, string name) { lock(_sync) { if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required."); var state = _state.Copy(); var snapshot = state.Snapshots.FirstOrDefault(item => item.Id == id); if(snapshot == null) throw new InvalidOperationException("The snapshot no longer exists."); snapshot.Name = name.Trim(); Save(state); } }
		public void DeleteSnapshot(string id) { lock(_sync) { var state = _state.Copy(); state.Snapshots.RemoveAll(snapshot => snapshot.Id == id); Save(state); } }
		private void PruneAutomaticSnapshots(DesktopRuleStateDocument state) {
			var maximum = Math.Max(0, Settings.GetInt("desktopRules.autoSnapshot.maximumCount", 3));
			var remove = state.Snapshots.Where(snapshot => snapshot.Kind == RuleSnapshotKind.Automatic).OrderBy(snapshot => snapshot.CreatedAtUtc).Take(Math.Max(0, state.Snapshots.Count(snapshot => snapshot.Kind == RuleSnapshotKind.Automatic) - maximum)).ToList();
			foreach(var snapshot in remove) state.Snapshots.Remove(snapshot);
		}
		private void Save(DesktopRuleStateDocument state) { DesktopRuleStateRepository.Validate(state); _repository.Save(state); _state = state; }
		private static DesktopRuleStateDocument MigrateLegacyState() {
			var state = new DesktopRuleStateDocument();
			try { state.CleanupRules = new WindowCleanupLockRepository().Load().Select(rule => rule.Copy()).ToList(); } catch(Exception e) { Logging.WriteLine("DesktopRuleState: could not migrate cleanup locks: " + e.Message); }
			try {
				foreach(var legacy in new DesktopLayoutSnapshotRepository().Load()) state.Snapshots.Add(new RuleSnapshot { Id = legacy.Id, Name = legacy.Name, CreatedAtUtc = legacy.UpdatedAtUtc == default(DateTime) ? legacy.CreatedAtUtc : legacy.UpdatedAtUtc, Kind = RuleSnapshotKind.Legacy, CleanupRulesAvailable = false, CleanupRules = new List<WindowCleanupLockRule>(), DesktopRules = legacy.Windows.Select(LegacyDesktopRule).ToList() });
			} catch(Exception e) { Logging.WriteLine("DesktopRuleState: could not migrate layout snapshots: " + e.Message); }
			return state;
		}
		private static DesktopRule LegacyDesktopRule(SnapshotWindow window) { return new DesktopRule { DesktopIndex = window.DesktopIndex, DesktopId = window.DesktopId, ProcessPath = window.ProcessPath, ProcessName = window.ProcessName, ApplicationNameIsOverride = window.ApplicationNameIsOverride, AppUserModelId = window.AppUserModelId, WindowClassName = window.WindowClassName, WindowTitle = window.WindowTitle, WindowTitleIsRegex = window.WindowTitleIsRegex, ProcessStartTimeUtc = window.ProcessStartTimeUtc }; }
	}

	public enum RuleRestoreStatus { CanRestore, AlreadyCorrect, NotFound, Ambiguous, Moved, Failed }
	public sealed class RuleRestoreItem { public DesktopRule Rule; public ApplicationWindow CurrentWindow; public int TargetDesktopIndex; public int CurrentDesktopIndex; public RuleRestoreStatus Status; public string Reason; }
	public sealed class RuleRestorePreview { public List<RuleRestoreItem> Items = new List<RuleRestoreItem>(); public int Count(RuleRestoreStatus status) { return Items.Count(item => item.Status == status); } }
	public sealed class DesktopRuleUpdateResult { public int UpdatedCount; public int AddedCount; public List<string> Conflicts = new List<string>(); }

	public sealed class DesktopRuleService {
		private sealed class Candidate { public ApplicationWindow Window; public int DesktopIndex; public int Score; }
		private readonly App _app;
		private readonly DesktopRuleStateService _state;
		internal DesktopRuleService(App app, DesktopRuleStateService state) { _app = app; _state = state; }
		public List<DesktopRule> List() { return _state.ListDesktopRules(); }
		public void ReplaceAll(IEnumerable<DesktopRule> rules) { _state.ReplaceDesktopRules(rules); }
		public DesktopRuleUpdateResult UpdateFromOpenWindows() {
			var rules = List(); var current = GetCurrentWindows(); var result = new DesktopRuleUpdateResult(); var additions = new List<DesktopRule>();
			foreach(var window in current) {
				var matches = rules.Where(existingRule => Matches(existingRule, window.Item1)).ToList();
				if(matches.Count == 0) { additions.Add(FromWindow(window.Item1, window.Item2, window.Item3)); continue; }
				if(matches.Count != 1) { result.Conflicts.Add(window.Item1.Title + " matches multiple desktop rules."); continue; }
				var matchedRule = matches[0];
				var windowsForRule = current.Where(candidate => Matches(matchedRule, candidate.Item1)).ToList();
				if(windowsForRule.Count != 1) { result.Conflicts.Add(matchedRule.DisplayName + " matches multiple open windows."); continue; }
				if(matchedRule.DesktopIndex != window.Item2 || !Same(matchedRule.DesktopId, window.Item3.ToString())) { matchedRule.DesktopIndex = window.Item2; matchedRule.DesktopId = window.Item3.ToString(); result.UpdatedCount++; }
			}
			rules.AddRange(additions); result.AddedCount = additions.Count; ReplaceAll(rules); return result;
		}
		public RuleRestorePreview Analyze() {
			var preview = new RuleRestorePreview(); var desktopIndices = VirtualDesktopRegistry.GetDesktopIndices(); var current = GetCurrentWindows().Select(item => Tuple.Create(item.Item1, item.Item2)).ToList(); var reserved = new HashSet<IntPtr>();
			foreach(var rule in List()) {
				var target = ResolveTarget(rule, desktopIndices); var matches = SelectMatches(rule, current.Where(candidate => !reserved.Contains(candidate.Item1.Handle)).ToList(), out var error); var restoreItem = new RuleRestoreItem { Rule = rule, TargetDesktopIndex = target };
				if(matches.Count == 0) { restoreItem.Status = RuleRestoreStatus.NotFound; restoreItem.Reason = error ?? "No reliably matching open window was found."; }
				else if(matches.Count > 1 && matches[0].Score == matches[1].Score) { restoreItem.Status = RuleRestoreStatus.Ambiguous; restoreItem.Reason = "More than one open window matches this rule."; }
				else { restoreItem.CurrentWindow = matches[0].Window; restoreItem.CurrentDesktopIndex = matches[0].DesktopIndex; reserved.Add(restoreItem.CurrentWindow.Handle); restoreItem.Status = restoreItem.CurrentDesktopIndex == target ? RuleRestoreStatus.AlreadyCorrect : RuleRestoreStatus.CanRestore; restoreItem.Reason = restoreItem.Status == RuleRestoreStatus.AlreadyCorrect ? "The window is already on its target desktop." : "The window can be moved to its target desktop."; }
				preview.Items.Add(restoreItem);
			}
			return preview;
		}
		public List<RuleRestoreItem> Apply(RuleRestorePreview preview, Action<int, int, RuleRestoreItem> progress) {
			var movable = preview.Items.Where(item => item.Status == RuleRestoreStatus.CanRestore).ToList(); var required = preview.Items.Count == 0 ? 0 : preview.Items.Max(item => item.TargetDesktopIndex); _app.EnsureDesktopCount(required + 1);
			for(var i = 0; i < movable.Count; i++) { var item = movable[i]; progress?.Invoke(i + 1, movable.Count, item); if(!WindowEnumerator.IsWindow(item.CurrentWindow.Handle)) { item.Status = RuleRestoreStatus.Failed; item.Reason = "The window closed before it could be moved."; continue; } string error; if(_app.TryMoveWindowToDesktop(item.CurrentWindow.Handle, item.TargetDesktopIndex, out error)) { item.Status = RuleRestoreStatus.Moved; item.Reason = "Moved to Desktop " + (item.TargetDesktopIndex + 1) + "."; } else { item.Status = RuleRestoreStatus.Failed; item.Reason = error; } }
			return preview.Items;
		}
		private static List<Candidate> SelectMatches(DesktopRule rule, List<Tuple<ApplicationWindow, int>> candidates, out string error) {
			error = null; var scored = candidates.Where(candidate => StableIdentityMatches(rule, candidate.Item1)).Select(candidate => new Candidate { Window = candidate.Item1, DesktopIndex = candidate.Item2, Score = Score(rule, candidate.Item1) }).ToList();
			if(rule.WindowTitleIsRegex) try { return scored.Where(candidate => Regex.IsMatch(candidate.Window.Title ?? "", rule.WindowTitle ?? "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))).OrderByDescending(candidate => candidate.Score).ToList(); } catch(ArgumentException) { error = "The saved window-title regular expression is invalid."; return new List<Candidate>(); } catch(RegexMatchTimeoutException) { error = "The saved window-title regular expression took too long to evaluate."; return new List<Candidate>(); }
			return scored.Where(candidate => Same(rule.WindowTitle, candidate.Window.Title)).OrderByDescending(candidate => candidate.Score).ToList();
		}
		private static int Score(DesktopRule rule, ApplicationWindow candidate) { var score = 0; if(rule.ApplicationNameIsOverride && Same(rule.ProcessName, candidate.ProcessName)) score += 100; else if(Same(rule.ProcessPath, candidate.ProcessPath)) score += 400; else if(Same(rule.AppUserModelId, candidate.AppUserModelId)) score += 300; else if(Same(rule.ProcessName, candidate.ProcessName) && Same(rule.WindowClassName, candidate.ClassName)) score += 100; if(rule.WindowTitleIsRegex || Same(rule.WindowTitle, candidate.Title)) score += 40; if(rule.ProcessStartTimeUtc.HasValue && candidate.ProcessStartTimeUtc.HasValue && rule.ProcessStartTimeUtc.Value == candidate.ProcessStartTimeUtc.Value) score += 20; return score; }
		private static bool StableIdentityMatches(DesktopRule rule, ApplicationWindow candidate) { if(rule.ApplicationNameIsOverride) return Same(rule.ProcessName, candidate.ProcessName); if(Same(rule.ProcessPath, candidate.ProcessPath)) return true; if(Same(rule.AppUserModelId, candidate.AppUserModelId)) return true; return Same(rule.ProcessName, candidate.ProcessName) && Same(rule.WindowClassName, candidate.ClassName); }
		private static bool Matches(DesktopRule rule, ApplicationWindow window) { return SelectMatches(rule, new List<Tuple<ApplicationWindow, int>> { Tuple.Create(window, 0) }, out var error).Count == 1 && error == null; }
		private static DesktopRule FromWindow(ApplicationWindow window, int desktopIndex, Guid desktopId) { return new DesktopRule { DesktopIndex = desktopIndex, DesktopId = desktopId.ToString(), ProcessPath = window.ProcessPath, ProcessName = window.ProcessName, AppUserModelId = window.AppUserModelId, WindowClassName = window.ClassName, WindowTitle = window.Title, ProcessStartTimeUtc = window.ProcessStartTimeUtc }; }
		private static int ResolveTarget(DesktopRule rule, Dictionary<Guid, int> desktopIndices) { Guid id; return Guid.TryParse(rule.DesktopId, out id) && desktopIndices.ContainsKey(id) ? desktopIndices[id] : rule.DesktopIndex; }
		private static bool Same(string left, string right) { return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
		private static List<Tuple<ApplicationWindow, int, Guid>> GetCurrentWindows() { var result = new List<Tuple<ApplicationWindow, int, Guid>>(); var desktopIndices = VirtualDesktopRegistry.GetDesktopIndices(); using(var lookup = new WindowDesktopLookup()) foreach(var window in WindowEnumerator.GetApplicationWindows()) { Guid id; int index; if(lookup.TryGetWindowDesktopId(window.Handle, out id) && desktopIndices.TryGetValue(id, out index)) result.Add(Tuple.Create(window, index, id)); } return result; }
	}

	public sealed class DesktopRuleSnapshotScheduler : IDisposable {
		private readonly DesktopRuleStateService _state;
		private readonly object _sync = new object();
		private Timer _timer;
		private bool _running;
		public DesktopRuleSnapshotScheduler(DesktopRuleStateService state) { _state = state ?? throw new ArgumentNullException("state"); }
		public void Reload() {
			lock(_sync) {
				if(_timer != null) { _timer.Dispose(); _timer = null; }
				if(!Settings.GetBool("desktopRules.autoSnapshot.enabled", false)) return;
				var minutes = Math.Max(1, Settings.GetInt("desktopRules.autoSnapshot.intervalMinutes", 30));
				var interval = TimeSpan.FromMinutes(minutes);
				_timer = new Timer(Capture, null, interval, interval);
			}
		}
		private void Capture(object state) {
			lock(_sync) {
				if(_running) return;
				_running = true;
			}
			try { _state.CreateSnapshot("Automatic " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), RuleSnapshotKind.Automatic); }
			catch(Exception e) { Logging.WriteLine("DesktopRuleSnapshotScheduler: " + e.Message); }
			finally { lock(_sync) _running = false; }
		}
		public void Dispose() { lock(_sync) { if(_timer != null) { _timer.Dispose(); _timer = null; } } }
	}
}
