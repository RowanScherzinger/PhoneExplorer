using Microsoft.Win32;

namespace PhoneExplorer.Hud.Themes;

/// <summary>
/// Windows 11: the default Fluent look of a Windows 11 app, in light or dark as Windows is set
/// (Settings › Personalization › Colors › app mode), with the user's accent colour. There is no
/// style guide for it: colours are mirrored by hand from WinUI's
/// controls/dev/CommonStyles/Common_themeresources_any.xaml in microsoft/microsoft-ui-xaml
/// (copied 2026-11-08; the "Default" dictionary is dark, "Light" is light), and type, corners and
/// timing from the Fluent 2 guidance for Windows (Segoe UI Variable, 4px control and 8px overlay
/// corners, 32px controls). Rebuilt by <see cref="Theme.Refresh"/> when the system setting changes.
/// </summary>
public static class Windows11
{
    public const string Id = "Windows";

    public static ThemeDef Create() => Create(SystemDark(), AccentPalette());

    /// <summary>Windows' app mode: AppsUseLightTheme = 0 means dark. Missing means light, the Windows default.</summary>
    static bool SystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// The accent shades Windows derives from the user's accent colour: Light3, Light2, Light1,
    /// Base, Dark1, Dark2, Dark3 (AccentPalette is eight RGBA entries in that order, then one
    /// unused). Falls back to the default Windows blue.
    /// </summary>
    static Color[] AccentPalette()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] p && p.Length >= 28)
                return Enumerable.Range(0, 7).Select(i => ThemeDef.Hex(p[i * 4] << 16 | p[i * 4 + 1] << 8 | p[i * 4 + 2])).ToArray();
        }
        catch { }
        return new[] { 0x99ebff, 0x60cdff, 0x0093f9, 0x0078d4, 0x005fb8, 0x003e92, 0x001968 }.Select(ThemeDef.Hex).ToArray();
    }

    static ThemeDef Create(bool dark, Color[] accent)
    {
        Color light3 = accent[0], light2 = accent[1], dark1 = accent[4], dark2 = accent[5];
        // Shorthand for a WinUI #AARRGGBB token.
        static Color Argb(uint argb) => ThemeDef.Rgba((int)(argb >> 16) & 0xff, (int)(argb >> 8) & 0xff, (int)argb & 0xff, (argb >> 24) / 255f);

        var bg = ThemeDef.Hex(dark ? 0x202020 : 0xf3f3f3);                      // SolidBackgroundFillColorBase
        var accentFill = dark ? light2 : dark1;                                     // AccentFillColorDefault
        var text = dark ? ThemeDef.Hex(0xffffff) : Argb(0xE4000000);               // TextFillColorPrimary
        var textSecondary = dark ? Argb(0xC5FFFFFF) : Argb(0x9E000000);
        var textTertiary = dark ? Argb(0x87FFFFFF) : Argb(0x72000000);
        var critical = ThemeDef.Hex(dark ? 0xff99a4 : 0xc42b1c);                   // SystemFillColorCritical

        var body = ThemeDef.Pick(new("Segoe UI Variable Text"), new("Segoe UI"));
        var strong = ThemeDef.Pick(new("Segoe UI Variable Text Semibold"), new("Segoe UI Semibold"));
        var display = ThemeDef.Pick(new("Segoe UI Variable Display Semib"), new("Segoe UI Semibold"));
        var mono = ThemeDef.Pick(new("Cascadia Mono"), new("Consolas"));

        return new ThemeDef
        {
            Id = Id,
            DisplayName = "Windows",
            Dark = dark,
            SystemChrome = true,

            Bg = bg,
            BgElevated = ThemeDef.Hex(dark ? 0x282828 : 0xf9f9f9),                  // SolidBackgroundFillColorTertiary
            Surface = dark ? Argb(0x0DFFFFFF) : Argb(0xB3FFFFFF),                   // CardBackgroundFillColorDefault
            SurfaceHover = dark ? Argb(0x15FFFFFF) : Argb(0x80F9F9F9),              // ControlFillColorSecondary
            SurfaceRaised = ThemeDef.Hex(dark ? 0x2c2c2c : 0xf9f9f9),               // flyout (acrylic fallback)
            Border = dark ? Argb(0x12FFFFFF) : Argb(0x0F000000),                    // ControlStrokeColorDefault
            BorderStrong = dark ? Argb(0x8BFFFFFF) : Argb(0x72000000),              // ControlStrongStrokeColorDefault
            Divider = dark ? Argb(0x15FFFFFF) : Argb(0x0F000000),                   // DividerStrokeColorDefault
            Text = text,
            TextStrong = text,
            TextMuted = textSecondary,
            TextDim = textTertiary,
            Accent = accentFill,
            AccentText = dark ? light3 : dark2,                                     // AccentTextFillColorPrimary
            AccentHover = Theme.Over(ThemeDef.Rgba(accentFill.R, accentFill.G, accentFill.B, 0.9f), bg), // AccentFillColorSecondary
            AccentSoft = ThemeDef.Rgba(accentFill.R, accentFill.G, accentFill.B, 0.12f),
            OnAccent = ThemeDef.Hex(dark ? 0x000000 : 0xffffff),                    // TextOnAccentFillColorPrimary
            // Numbers in the primary text colour; the accent marks selection, as in File Explorer.
            Highlight = text,
            HighlightSoft = dark ? Argb(0x0FFFFFFF) : Argb(0x09000000),             // SubtleFillColorSecondary
            Select = accentFill,
            SelectSoft = ThemeDef.Rgba(accentFill.R, accentFill.G, accentFill.B, 0.22f),
            SelectRing = accentFill,
            Progress = accentFill,
            // Section headers are BodyStrong in the primary text colour.
            Eyebrow = text,
            StatusOk = ThemeDef.Hex(dark ? 0x6ccb5f : 0x0f7b0f),                    // SystemFillColorSuccess
            StatusPending = accentFill,                                             // SystemFillColorAttention
            StatusIdle = ThemeDef.Hex(dark ? 0x9d9d9d : 0x8a8a8a),                  // SystemFillColorSolidNeutral
            StatusWarning = ThemeDef.Hex(dark ? 0xfce100 : 0x9d5d00),               // SystemFillColorCaution
            StatusDanger = critical,
            DangerText = critical,
            DangerFill = ThemeDef.Hex(dark ? 0x442726 : 0xfde7e9),                  // SystemFillColorCriticalBackground
            DangerBorder = ThemeDef.Rgba(critical.R, critical.G, critical.B, 0.5f),
            Info = accentFill,
            FocusRing = dark ? ThemeDef.Hex(0xffffff) : Argb(0xE4000000),          // FocusStrokeColorOuter
            InputFill = dark ? Argb(0xB31E1E1E) : ThemeDef.Hex(0xffffff),          // ControlFillColorInputActive
            // Badges over photos sit on a chip of the window's own colour, so badge text keeps its contrast.
            Scrim = dark ? ThemeDef.Rgba(32, 32, 32, 0.80f) : ThemeDef.Rgba(249, 249, 249, 0.88f),
            NeutralData = textSecondary,

            Body = body,
            Sans = body,
            Mono = mono,
            Label = strong,
            Heading = display,
            Button = body,
            Number = body,
            BigNumber = display,
            // Fluent type ramp: Caption 12, Body 14, Body Large 18, Subtitle 20, Title 28, Title Large 40.
            Text2xs = 12, TextXs = 12, TextSm = 12, TextMd = 14, TextLg = 18, TextXl = 20, Text2xl = 28, Text3xl = 40,
            TextTitle = 20,
            TrackingLabel = 0f,
            TrackingWide = 0f,
            Uppercase = false,

            RadiusSm = 4, RadiusMd = 8, RadiusLg = 8, RadiusXl = 8,   // ControlCornerRadius, OverlayCornerRadius
            Pills = false,
            ControlHeight = 32,
            PrimaryHeight = 32,
            DurationFast = 167,    // ControlFastAnimationDuration
            DurationNormal = 250,  // ControlNormalAnimationDuration
            LoopPulse = 2000,
            Hud = false,
            Cute = false,
        };
    }
}
