using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsVirtualDesktopHelper.Util {

	class Icons {

		// Icons are cached as ready-to-use Icon objects: the cache is bounded by the number of
		// distinct texts/sizes/themes/styles, and re-using the same Icon instance avoids creating
		// a new GDI icon handle on every tray icon update (which would leak, see DestroyIcon below)
		private static ConcurrentDictionary<string, Icon> _cache = new ConcurrentDictionary<string, Icon>();

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool DestroyIcon(IntPtr hIcon);

		public static Icon GenerateNotificationIcon(string text, string theme, int dpi, bool drawAsSymbol, double opacity = 1.0) {
			// Init
			var size = 16;
			if (dpi > 96) size = 64;
			var renderSize = 128; // GDI has really weak text drawing on transparent, so to get best results we render large then downscale...
			var textToRender = text;
			if (textToRender == null) textToRender = ""; // sanity
			var textToRenderInfo = new StringInfo(textToRender);
			if (textToRenderInfo.LengthInTextElements > 2) textToRender = new StringInfo(textToRender).SubstringByTextElements(0, 2);
			var textElementCount = textToRenderInfo.LengthInTextElements;
			var textToRenderSizeRatio = textElementCount == 1 ? 0.45f : 0.38f;
			var automaticFontSizeFitTolerance = 0.0f;
			var offsetY = 0.0f;
			var fontFamily = Settings.GetFontName("theme.icons.font");
			var fontStyle = Settings.GetFontStyle("theme.icons.font");
			if(Util.Emoji.HasEmoji(textToRender)) {
				fontFamily = Settings.GetFontName("theme.icons.emojiFont");
				fontStyle = Settings.GetFontStyle("theme.icons.emojiFont");
			}
			if (drawAsSymbol) {
				fontFamily = Settings.GetFontName("theme.icons.symbolsFont");
				fontStyle = Settings.GetFontStyle("theme.icons.symbolsFont");
				textToRenderSizeRatio = 1.8f;
				if (dpi > 96) textToRenderSizeRatio = 1.0f;
				automaticFontSizeFitTolerance = 2.0f;
				offsetY = -0.4f;
			}
			var textSize = renderSize * textToRenderSizeRatio;
			var bgColorSetting = Settings.GetString("theme.icons.iconBG." + theme);
			var fgColorSetting = drawAsSymbol ? Settings.GetString("theme.icons.symbolFG." + theme) : Settings.GetString("theme.icons.iconFG." + theme);

			// Cache hit?
			var cacheKey = textToRender + "_" + textSize + "_" + size + "_" + theme + "_" + fontFamily + "_" + fontStyle + "_" + bgColorSetting + "_" + fgColorSetting + "_" + drawAsSymbol + "_" + opacity;
			Icon cachedIcon;
			if (_cache.TryGetValue(cacheKey, out cachedIcon)) {
				return cachedIcon;
			}

			// Theme
			var configuredBgColor = ColorTranslator.FromHtml(bgColorSetting);
			var configuredFgColor = ColorTranslator.FromHtml(fgColorSetting);
			var bgColor = configuredBgColor;
			var fgColor = configuredFgColor;
			if(!drawAsSymbol) {
				UseBadgePalette(theme, configuredBgColor, configuredFgColor, out bgColor, out fgColor);
			}
			if (opacity != 1.0) fgColor = Color.FromArgb((int)(255.0f * opacity), fgColor);
			if (opacity != 1.0) bgColor = Color.FromArgb((int)(255.0f * opacity), bgColor);

			Icon icon;
			using (var bitmap = new Bitmap(renderSize, renderSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
			using (var bitmapScaledDown = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
				// Draw icon
				using (var g = Graphics.FromImage(bitmap))
				using (var fgBrush = new SolidBrush(fgColor))
				using (var format = new StringFormat()) {
					g.CompositingQuality = CompositingQuality.HighQuality;
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

					g.Clear(Color.Transparent);
					if(!drawAsSymbol) {
						DrawBadgeBackground(g, renderSize, bgColor);
					}

					format.Alignment = StringAlignment.Center;
					format.LineAlignment = StringAlignment.Center;

					var rect = new Rectangle(
						-renderSize / 2, // x
						(int)(renderSize * offsetY), // y
						renderSize * 2, // w (note: we oversize the drawing area so that we never clip text to new lines...
						renderSize + (int)(-1 * renderSize * offsetY)); // h

					var font = new Font(fontFamily, textSize, fontStyle);
					try {
						// Automatic text fit
						var fontScaleDownFactor = 1.0f;
						var fontScaleDownAttempts = 0;
						while (fontScaleDownAttempts < 10 && fontScaleDownFactor > 0) {
							fontScaleDownAttempts++;
							fontScaleDownFactor -= 0.1f;
							var measure = g.MeasureString(textToRender, font);
							if (measure.Width > renderSize * (1.0f + automaticFontSizeFitTolerance)) {
								font.Dispose();
								font = new Font(fontFamily, textSize * fontScaleDownFactor, fontStyle);
							} else {
								break;
							}
						}
						g.DrawString(textToRender, font, fgBrush, rect, format);
						g.Flush();
					} finally {
						font.Dispose();
					}
				}

				// Scale down
				using (var g = Graphics.FromImage(bitmapScaledDown)) {
					g.CompositingQuality = CompositingQuality.HighQuality;
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.DrawImage(bitmap, 0, 0, size, size);
					g.Flush();
				}

				// Create the icon: GetHicon() allocates a native GDI icon handle which Icon.FromHandle
				// does not take ownership of - we clone to an Icon which owns its own handle and then
				// destroy the original handle, otherwise every icon update leaks a GDI handle until
				// the process hits the GDI object limit
				var hIcon = bitmapScaledDown.GetHicon();
				try {
					using (var tempIcon = Icon.FromHandle(hIcon)) {
						icon = (Icon)tempIcon.Clone();
					}
				} finally {
					DestroyIcon(hIcon);
				}
			}

			// Register in cache
			_cache[cacheKey] = icon;

			return icon;
		}

		private static void UseBadgePalette(string theme, Color configuredBgColor, Color configuredFgColor, out Color bgColor, out Color fgColor) {
			var defaultAccent = Color.FromArgb(0, 120, 212);
			var isDefaultDark = configuredBgColor.ToArgb() == Color.Black.ToArgb() && configuredFgColor.ToArgb() == Color.White.ToArgb();
			var isDefaultLight = configuredBgColor.ToArgb() == Color.White.ToArgb() && configuredFgColor.ToArgb() == Color.Black.ToArgb();
			var isDefaultBlue = configuredBgColor.ToArgb() == defaultAccent.ToArgb() && configuredFgColor.ToArgb() == Color.White.ToArgb();
			var useDefaultFluentPalette = isDefaultDark || isDefaultLight || isDefaultBlue;

			if(isDefaultBlue) {
				bgColor = defaultAccent;
				fgColor = Color.White;
				return;
			}

			if(useDefaultFluentPalette && theme == "light") {
				bgColor = defaultAccent;
				fgColor = Color.White;
				return;
			}

			if(useDefaultFluentPalette) {
				bgColor = defaultAccent;
				fgColor = Color.White;
				return;
			}

			bgColor = configuredBgColor;
			fgColor = configuredFgColor;
		}

		private static void DrawBadgeBackground(Graphics g, int renderSize, Color bgColor) {
			var inset = renderSize * 0.025f;
			var rect = new RectangleF(inset, inset, renderSize - inset * 2, renderSize - inset * 2);

			using (var bgBrush = new SolidBrush(bgColor)) {
				g.FillRectangle(bgBrush, rect);
			}
		}

	}
}
