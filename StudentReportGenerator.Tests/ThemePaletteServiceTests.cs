using System.Linq;
using System.Windows.Media;
using StudentReportGenerator.Services;
using Xunit;

namespace StudentReportGenerator.Tests
{
    public class ThemePaletteServiceTests
    {
        // The five original branding presets plus a sample of the new brighter swatches
        public static TheoryData<string> PresetHexes => new()
        {
            "#FF392A4C", "#FF0A192F", "#FF1B3822", "#FF4A1525", "#FF263238",
            "#FF00695C", "#FF1565C0", "#FFC2185B", "#FFEF6C00",
        };

        [Fact]
        public void LightAndDarkPalettes_HaveIdenticalKeySets()
        {
            // Codifies the old "every App.xaml key must exist in both dictionaries" comment:
            // a missing key means toggling Dark Mode leaves that element on a stale brush.
            Assert.Equal(
                ThemePaletteService.LightPalette.Keys.OrderBy(k => k),
                ThemePaletteService.DarkPalette.Keys.OrderBy(k => k));
        }

        [Fact]
        public void Palettes_ContainOnlyParseableColours()
        {
            foreach (var value in ThemePaletteService.LightPalette.Values.Concat(ThemePaletteService.DarkPalette.Values))
                Assert.NotNull(ThemePaletteService.TryParseHex(value));
        }

        [Theory]
        [MemberData(nameof(PresetHexes))]
        public void DarkModeAccent_IsLightenedEnoughToRead(string hex)
        {
            var accent = ThemePaletteService.TryParseHex(hex)!.Value;

            var palette = ThemePaletteService.BuildAccentPalette(accent, isDark: true);

            Assert.True(ThemePaletteService.RelativeLuminance(palette["ThemeAccent"]) >= 0.35,
                $"{hex} stayed too dark for a dark background");
        }

        [Theory]
        [MemberData(nameof(PresetHexes))]
        public void AccentText_IsReadableOnAccent_InBothModes(string hex)
        {
            var accent = ThemePaletteService.TryParseHex(hex)!.Value;

            foreach (bool isDark in new[] { false, true })
            {
                var palette = ThemePaletteService.BuildAccentPalette(accent, isDark);
                double surface = ThemePaletteService.RelativeLuminance(palette["ThemeAccent"]);
                double text = ThemePaletteService.RelativeLuminance(palette["ThemeAccentText"]);

                // Dark text on light surfaces, light text on dark surfaces
                Assert.True(surface > 0.45 ? text < 0.2 : text > 0.8,
                    $"{hex} (dark={isDark}): accent luminance {surface:F2} got text luminance {text:F2}");
            }
        }

        [Fact]
        public void LightModeAccent_TooPaleForWhiteBackground_IsDarkened()
        {
            var paleYellow = ThemePaletteService.TryParseHex("#FFF5E050")!.Value;

            var palette = ThemePaletteService.BuildAccentPalette(paleYellow, isDark: false);

            Assert.True(ThemePaletteService.RelativeLuminance(palette["ThemeAccent"]) <= 0.60,
                "a near-white accent must be darkened to stay legible as header text on white");
        }

        [Fact]
        public void NavColours_KeepTheRawBrandingColour()
        {
            var navy = ThemePaletteService.TryParseHex("#FF0A192F")!.Value;

            var palette = ThemePaletteService.BuildAccentPalette(navy, isDark: true);

            Assert.Equal(navy, palette["ThemeAccentNav"]);           // never contrast-adjusted
            Assert.Equal(Colors.White, palette["ThemeAccentNavText"]); // white on a dark navy bar
        }

        [Fact]
        public void Mix_Endpoints_ReturnInputColours()
        {
            Assert.Equal(Colors.Black, ThemePaletteService.Mix(Colors.Black, Colors.White, 0));
            Assert.Equal(Colors.White, ThemePaletteService.Mix(Colors.Black, Colors.White, 1));
        }

        [Fact]
        public void BackgroundTint_ShiftsBaseOnlySlightly()
        {
            var baseBg = ThemePaletteService.TryParseHex("#FFFAFAFA")!.Value;
            var tint = ThemePaletteService.TryParseHex("#FF3C78B9")!.Value;

            var tinted = ThemePaletteService.ApplyBackgroundTint(baseBg, tint);

            Assert.NotEqual(baseBg, tinted);
            // Strength is clamped small, so the background stays close to the original —
            // ThemeText contrast is never at risk.
            Assert.True(ThemePaletteService.RelativeLuminance(tinted) > 0.8);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a colour")]
        [InlineData("#GGHHII")]
        public void TryParseHex_InvalidInput_ReturnsNullNotThrow(string? hex)
        {
            Assert.Null(ThemePaletteService.TryParseHex(hex!));
        }

        [Fact]
        public void TryParseHex_ValidForms_Parse()
        {
            Assert.NotNull(ThemePaletteService.TryParseHex("#1565C0"));
            Assert.NotNull(ThemePaletteService.TryParseHex("#FF1565C0"));
            Assert.NotNull(ThemePaletteService.TryParseHex(" #ABC "));
        }
    }
}
