using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Pure colour maths for the app's theming: the base light/dark palettes (formerly inline in
    /// SettingsViewModel), plus derivation of the accent brush family from whatever colour the
    /// teacher picks — contrast-adjusted per mode so a school's navy or burgundy stays readable as
    /// section headers and buttons in both light and dark themes. No WPF resource access here, so
    /// every rule is unit-testable.
    /// </summary>
    public static class ThemePaletteService
    {
        // Every key declared in App.xaml (other than the accent family, which is computed) MUST
        // have an entry in both dictionaries below, or toggling Dark Mode leaves that element
        // showing its stale previous brush. ThemePaletteServiceTests enforces the parity.
        public static readonly Dictionary<string, string> LightPalette = new()
        {
            ["ThemeAppBg"] = "#FFFAFAFA",
            ["ThemeCardBg"] = "#FFFFFFFF",
            ["ThemeText"] = "#FF333333",
            ["ThemeMutedText"] = "#FF616161",
            ["ThemeBorder"] = "#FFDDDDDD",
            ["ThemeInputBg"] = "#FFFFFFFF",
            ["ThemePreviewBg"] = "#FFF9F9F9",
            ["ThemeButtonBg"] = "#FFEEEEEE",
            ["ThemeButtonHoverBg"] = "#FFDDDDDD",
            ["ThemePrimaryBtnBg"] = "#FF4CAF50",
            ["ThemePrimaryBtnHoverBg"] = "#FF43A047",
            ["ThemeDangerBg"] = "#FFFFEBEE",
            ["ThemeDangerText"] = "#FFD32F2F",
            ["ThemeDangerStrongBg"] = "#FFF44336",
            ["ThemeInfoBg"] = "#FFE0F7FA",
            ["ThemeInfoText"] = "#FF00838F",
            ["ThemeInfoAccent"] = "#FF1E88E5",
            ["ThemeWarningBg"] = "#FFFFF3E0",
            ["ThemeWarningText"] = "#FFE65100",
            ["ThemeWarningAccent"] = "#FFF57C00",
            ["ThemeSuccessBg"] = "#FFE8F5E9",
            ["ThemeSuccessText"] = "#FF2E7D32",
            ["ThemeSuccessAccent"] = "#FF43A047",
            ["ThemeAccentBlueBg"] = "#FF1976D2",
            ["ThemeMetricCardBg"] = "#FFF5F5F5",
            ["ThemeMetricIndigoBg"] = "#FFE8EAF6",
            ["ThemeMetricNvidiaBg"] = "#FFE1F5FE",
            ["ThemeMetricNvidiaText"] = "#FF01579B",
            ["ThemeMetricGeminiBg"] = "#FFE0F7FA",
            ["ThemeMetricGeminiText"] = "#FF006064",
            ["ThemeMetricOpenAiBg"] = "#FFF3E5F5",
            ["ThemeMetricOpenAiText"] = "#FF4A148C",
            ["ThemeMetricClaudeBg"] = "#FFFFF3E0",
            ["ThemeMetricClaudeText"] = "#FFE65100",
        };

        public static readonly Dictionary<string, string> DarkPalette = new()
        {
            ["ThemeAppBg"] = "#FF121212",
            ["ThemeCardBg"] = "#FF1E1E1E",
            ["ThemeText"] = "#FFE0E0E0",
            ["ThemeMutedText"] = "#FFAAAAAA",
            ["ThemeBorder"] = "#FF333333",
            ["ThemeInputBg"] = "#FF2D2D2D",
            ["ThemePreviewBg"] = "#FF252525",
            ["ThemeButtonBg"] = "#FF2F2F2F",
            ["ThemeButtonHoverBg"] = "#FF3D3D3D",
            ["ThemePrimaryBtnBg"] = "#FF388E3C",
            ["ThemePrimaryBtnHoverBg"] = "#FF2E7D32",
            ["ThemeDangerBg"] = "#FF3B2226",
            ["ThemeDangerText"] = "#FFEF9A9A",
            ["ThemeDangerStrongBg"] = "#FFD32F2F",
            ["ThemeInfoBg"] = "#FF0F2E33",
            ["ThemeInfoText"] = "#FF4DD0E1",
            ["ThemeInfoAccent"] = "#FF64B5F6",
            ["ThemeWarningBg"] = "#FF3A2A16",
            ["ThemeWarningText"] = "#FFFFB74D",
            ["ThemeWarningAccent"] = "#FFFFB74D",
            ["ThemeSuccessBg"] = "#FF1B2E1F",
            ["ThemeSuccessText"] = "#FF81C784",
            ["ThemeSuccessAccent"] = "#FF81C784",
            ["ThemeAccentBlueBg"] = "#FF1565C0",
            ["ThemeMetricCardBg"] = "#FF232323",
            ["ThemeMetricIndigoBg"] = "#FF23263A",
            ["ThemeMetricNvidiaBg"] = "#FF12293A",
            ["ThemeMetricNvidiaText"] = "#FF81D4FA",
            ["ThemeMetricGeminiBg"] = "#FF103235",
            ["ThemeMetricGeminiText"] = "#FF80DEEA",
            ["ThemeMetricOpenAiBg"] = "#FF2A1D31",
            ["ThemeMetricOpenAiText"] = "#FFCE93D8",
            ["ThemeMetricClaudeBg"] = "#FF33270F",
            ["ThemeMetricClaudeText"] = "#FFFFCC80",
        };

        /// <summary>
        /// Derives the accent brush family from the teacher's chosen colour:
        /// <c>ThemeAccent</c> (headers/primary buttons; lightened in dark mode, darkened if too pale
        /// for a light background), <c>ThemeAccentHover</c>, <c>ThemeAccentSubtle</c> (a faint tint
        /// for highlight backgrounds), <c>ThemeAccentText</c> (readable text ON the accent), and the
        /// nav-bar pair <c>ThemeAccentNav</c>/<c>ThemeAccentNavText</c>, which keep the school's raw
        /// branding colour untouched.
        /// </summary>
        public static Dictionary<string, Color> BuildAccentPalette(Color accent, bool isDark)
        {
            Color adjusted = accent;
            if (isDark)
            {
                // Dark cards/backgrounds: lighten dark school colours until they read clearly
                while (RelativeLuminance(adjusted) < 0.35 && adjusted != Colors.White)
                    adjusted = Mix(adjusted, Colors.White, 0.15);
            }
            else
            {
                // Light backgrounds: darken very pale accents until headers stay legible
                while (RelativeLuminance(adjusted) > 0.60 && adjusted != Colors.Black)
                    adjusted = Mix(adjusted, Colors.Black, 0.15);
            }

            Color hover = isDark ? Mix(adjusted, Colors.White, 0.12) : Mix(adjusted, Colors.Black, 0.15);
            Color subtle = isDark
                ? Mix(adjusted, (Color)ColorConverter.ConvertFromString("#FF1E1E1E"), 0.82)
                : Mix(adjusted, Colors.White, 0.88);

            return new Dictionary<string, Color>
            {
                ["ThemeAccent"] = adjusted,
                ["ThemeAccentHover"] = hover,
                ["ThemeAccentSubtle"] = subtle,
                ["ThemeAccentText"] = TextOn(adjusted),
                ["ThemeAccentNav"] = accent,
                ["ThemeAccentNavText"] = TextOn(accent),
            };
        }

        /// <summary>Near-black on light surfaces, white on dark ones (WCAG-luminance based).</summary>
        public static Color TextOn(Color surface) =>
            RelativeLuminance(surface) > 0.45
                ? (Color)ColorConverter.ConvertFromString("#FF1A1A1A")
                : Colors.White;

        /// <summary>WCAG relative luminance, 0 (black) to 1 (white).</summary>
        public static double RelativeLuminance(Color c)
        {
            static double Channel(byte v)
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }

        /// <summary>Linear blend of <paramref name="a"/> toward <paramref name="b"/> by
        /// <paramref name="t"/> (0 = all a, 1 = all b). Alpha is kept opaque.</summary>
        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>Faintly tints a base background toward the given colour — the optional
        /// "background tint" personalisation. Strength is deliberately small so text contrast
        /// against <c>ThemeText</c> is never at risk.</summary>
        public static Color ApplyBackgroundTint(Color baseBg, Color tint, double strength = 0.06) =>
            Mix(baseBg, tint, Math.Clamp(strength, 0, 0.15));

        /// <summary>Parses a hex colour string ("#RGB", "#RRGGBB" or "#AARRGGBB"), returning null
        /// rather than throwing on anything invalid — typed hex input must never crash theming.</summary>
        public static Color? TryParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            try
            {
                return ColorConverter.ConvertFromString(hex.Trim()) is Color c ? c : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
