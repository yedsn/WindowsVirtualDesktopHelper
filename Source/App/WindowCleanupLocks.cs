using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using WindowsVirtualDesktopHelper.Util;

namespace WindowsVirtualDesktopHelper {
	public sealed class WindowCleanupBatch {
		public int DesktopIndex { get; private set; }
		public List<ApplicationWindow> Windows { get; private set; }

		public WindowCleanupBatch(int desktopIndex, IEnumerable<ApplicationWindow> windows) {
			DesktopIndex = desktopIndex;
			Windows = windows == null ? new List<ApplicationWindow>() : windows.ToList();
		}
	}

	public sealed class WindowCleanupResult {
		public int DesktopIndex { get; private set; }
		public int RequestedCloseCount { get; private set; }
		public int FailedCloseCount { get; private set; }
		public int AccessDeniedCloseCount { get; private set; }

		public WindowCleanupResult(int desktopIndex, int requestedCloseCount, int failedCloseCount, int accessDeniedCloseCount = 0) {
			DesktopIndex = desktopIndex;
			RequestedCloseCount = requestedCloseCount;
			FailedCloseCount = failedCloseCount;
			AccessDeniedCloseCount = accessDeniedCloseCount;
		}
	}

	[DataContract]
	public sealed class WindowCleanupLockRule {
		[DataMember] public string ProcessName;
		[DataMember] public string WindowTitle;
		[DataMember(EmitDefaultValue = false)] public bool WindowTitleIsRegex;

		public WindowCleanupLockRule() { }

		public WindowCleanupLockRule(ApplicationWindow window) {
			ProcessName = window.ProcessName;
			WindowTitle = window.Title;
		}

		public bool Matches(ApplicationWindow window) {
			if(window == null || !Same(ProcessName, window.ProcessName)) return false;
			if(!WindowTitleIsRegex) return string.Equals(WindowTitle ?? "", window.Title ?? "", StringComparison.OrdinalIgnoreCase);
			try {
				return Regex.IsMatch(window.Title ?? "", WindowTitle ?? "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
			} catch(ArgumentException) {
				return false;
			} catch(RegexMatchTimeoutException) {
				return false;
			}
		}

		public WindowCleanupLockRule Copy() {
			return new WindowCleanupLockRule { ProcessName = ProcessName, WindowTitle = WindowTitle, WindowTitleIsRegex = WindowTitleIsRegex };
		}

		private static bool Same(string left, string right) { return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
	}

	[DataContract]
	public sealed class WindowCleanupLockDocument {
		[DataMember] public int Version = 1;
		[DataMember] public List<WindowCleanupLockRule> Rules = new List<WindowCleanupLockRule>();
	}

	public sealed class WindowCleanupLockRepository {
		private const int SupportedVersion = 1;
		private readonly string _path;

		public WindowCleanupLockRepository() {
			var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsVirtualDesktopHelper");
			_path = Path.Combine(directory, "window-cleanup-locks.json");
		}

		public WindowCleanupLockRepository(string path) {
			if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A cleanup lock file path is required.");
			_path = path;
		}

		public List<WindowCleanupLockRule> Load() {
			if(!File.Exists(_path)) return new List<WindowCleanupLockRule>();
			try {
				using(var stream = File.OpenRead(_path)) {
					var document = (WindowCleanupLockDocument)new DataContractJsonSerializer(typeof(WindowCleanupLockDocument)).ReadObject(stream);
					if(document == null || document.Version != SupportedVersion || document.Rules == null) throw new InvalidDataException("The cleanup lock file has an unsupported format.");
					Validate(document.Rules);
					return document.Rules;
				}
			} catch(Exception e) when(e is SerializationException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException) {
				throw new InvalidOperationException("Could not read saved cleanup locks. The saved file was not changed.", e);
			}
		}

		public void Save(List<WindowCleanupLockRule> rules) {
			if(rules == null) throw new ArgumentNullException("rules");
			Validate(rules);
			Directory.CreateDirectory(Path.GetDirectoryName(_path));
			var temporaryPath = _path + ".tmp";
			try {
				using(var stream = File.Create(temporaryPath)) new DataContractJsonSerializer(typeof(WindowCleanupLockDocument)).WriteObject(stream, new WindowCleanupLockDocument { Rules = rules });
				if(File.Exists(_path)) File.Replace(temporaryPath, _path, null); else File.Move(temporaryPath, _path);
			} catch {
				if(File.Exists(temporaryPath)) File.Delete(temporaryPath);
				throw;
			}
		}

		private static void Validate(IEnumerable<WindowCleanupLockRule> rules) {
			if(rules.Any(rule => rule == null || string.IsNullOrWhiteSpace(rule.ProcessName))) throw new InvalidDataException("A cleanup lock rule is incomplete.");
			foreach(var rule in rules.Where(rule => rule.WindowTitleIsRegex)) {
				try {
					new Regex(rule.WindowTitle ?? "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
				} catch(ArgumentException e) {
					throw new InvalidDataException("A cleanup lock title regular expression is invalid.", e);
				}
			}
		}
	}

	public sealed class WindowCleanupLockService {
		private readonly object _sync = new object();
		private readonly DesktopRuleStateService _state;
		private List<WindowCleanupLockRule> _rules;

		public WindowCleanupLockService() : this(new DesktopRuleStateService()) { }

		public WindowCleanupLockService(DesktopRuleStateService state) {
			_state = state ?? throw new ArgumentNullException("state");
			Reload();
		}

		public void Reload() {
			lock(_sync) _rules = _state.ListCleanupRules();
		}

		public bool IsProtected(ApplicationWindow window) {
			if(window == null) return false;
			lock(_sync) return _rules.Any(rule => rule.Matches(window));
		}

		public List<WindowCleanupLockRule> List() {
			lock(_sync) return _rules.Select(rule => rule.Copy()).ToList();
		}

		public void ReplaceAll(IEnumerable<WindowCleanupLockRule> rules) {
			if(rules == null) throw new ArgumentNullException("rules");
			lock(_sync) Save(rules.Select(rule => rule == null ? null : rule.Copy()).ToList());
		}

		public bool Matches(ApplicationWindow expected, ApplicationWindow actual) {
			return expected != null && actual != null && expected.Handle == actual.Handle && expected.ProcessId == actual.ProcessId
				&& (!expected.ProcessStartTimeUtc.HasValue || !actual.ProcessStartTimeUtc.HasValue || expected.ProcessStartTimeUtc.Value == actual.ProcessStartTimeUtc.Value);
		}

		public bool Toggle(ApplicationWindow window) {
			if(window == null) return false;
			lock(_sync) {
				var rules = _rules.ToList();
				var existing = rules.Where(rule => rule.Matches(window)).ToList();
				if(existing.Count > 0) {
					rules.RemoveAll(rule => existing.Contains(rule));
					Save(rules);
					return false;
				}
				rules.Add(new WindowCleanupLockRule(window));
				Save(rules);
				return true;
			}
		}

		public int Protect(IEnumerable<ApplicationWindow> windows) {
			if(windows == null) return 0;
			lock(_sync) {
				var rules = _rules.ToList();
				var added = 0;
				foreach(var window in windows) if(window != null && !rules.Any(rule => rule.Matches(window))) {
					rules.Add(new WindowCleanupLockRule(window));
					added++;
				}
				if(added > 0) Save(rules);
				return added;
			}
		}

		private void Save(List<WindowCleanupLockRule> rules) {
			_state.ReplaceCleanupRules(rules);
			_rules = rules.Select(rule => rule.Copy()).ToList();
		}
	}
}
