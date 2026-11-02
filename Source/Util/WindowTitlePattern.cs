using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper.Util {
	internal static class WindowTitlePattern {
		internal static void Bind(CheckBox useRegularExpression, TextBox windowTitle) {
			// Click handles user toggles without converting titles when a saved rule is loaded.
			useRegularExpression.Click += (sender, e) => {
				windowTitle.Text = useRegularExpression.Checked ? Regex.Escape(windowTitle.Text) : UnescapeLiteral(windowTitle.Text);
			};
		}

		private static string UnescapeLiteral(string pattern) {
			// Undo Regex.Escape while preserving regex syntax that has no literal equivalent.
			return Regex.Replace(pattern, @"\\(.)", match => {
				var escaped = match.Groups[1].Value[0];
				switch(escaped) {
					case 'n': return "\n";
					case 'r': return "\r";
					case 't': return "\t";
					case 'f': return "\f";
				}
				return "\\*+?|{[()^$.# ".IndexOf(escaped) >= 0 ? escaped.ToString() : match.Value;
			});
		}
	}
}
