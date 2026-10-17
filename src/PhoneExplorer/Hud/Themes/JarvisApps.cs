namespace PhoneExplorer.Hud.Themes;

/// <summary>
/// Jarvis Apps: the copper-and-gold HUD. Mirrored by hand from
/// F:\Projects\StyleGuides\JarvisApps\tokens\tokens.css (copied 2026-10-17; see decisions/0008 in
/// the guide). When the guide changes, re-copy the values here.
/// </summary>
public static class JarvisApps
{
    public static ThemeDef Create()
    {
        // Palette (only what the semantic layer needs)
        var neutral950 = ThemeDef.Hex(0x060404);
        var neutral900 = ThemeDef.Hex(0x0e0a09);
        var neutral400 = ThemeDef.Hex(0x85796f);
        var neutral300 = ThemeDef.Hex(0xada197);
        var neutral100 = ThemeDef.Hex(0xded2c6);
        var neutral50 = ThemeDef.Hex(0xf2eae1);
        var copper500 = ThemeDef.Hex(0xb87333);
        var copper400 = ThemeDef.Hex(0xcf8c4e);
        var gold500 = ThemeDef.Hex(0xd4a017);
        var gold400 = ThemeDef.Hex(0xe6b93e);
        var gold300 = ThemeDef.Hex(0xf2d27a);
        var silver600 = ThemeDef.Hex(0x8c8c8c);
        var silver400 = ThemeDef.Hex(0xc0c0c0);
        var red500 = ThemeDef.Hex(0xdc143c);
        var red400 = ThemeDef.Hex(0xf0485f);
        var ember500 = ThemeDef.Hex(0xf07a2a);
        var chartreuse500 = ThemeDef.Hex(0xa3d21f);
        var arc400 = ThemeDef.Hex(0x6ec6ff);

        // --font-mono / --font-sans / --font-display, resolved to the first installed family.
        var mono = ThemeDef.Pick(new("Cascadia Mono"), new("Cascadia Code"), new("DM Mono"), new("Consolas"));
        var monoLight = ThemeDef.Pick(new("Cascadia Mono Light"), mono);
        var sans = ThemeDef.Pick(new("Inter"), new("Segoe UI Variable Text"), new("Segoe UI"));
        var display = ThemeDef.Pick(new("Orbitron"), new("Bahnschrift"));
        var displayBold = display.Family == "Bahnschrift" ? ThemeDef.Pick(new("Bahnschrift SemiBold"), display) : display;

        return new ThemeDef
        {
            Id = "JarvisApps",
            DisplayName = "Jarvis",

            Bg = neutral950,
            BgElevated = neutral900,
            Surface = Color.FromArgb(158, 14, 10, 9),          // rgba(14,10,9,.62)
            SurfaceHover = Color.FromArgb(191, 21, 16, 14),    // rgba(21,16,14,.75)
            SurfaceRaised = Color.FromArgb(224, 21, 16, 14),   // rgba(21,16,14,.88)
            Border = Color.FromArgb(56, 184, 115, 51),         // rgba(184,115,51,.22)
            BorderStrong = Color.FromArgb(128, 184, 115, 51),  // rgba(184,115,51,.50)
            Divider = Color.FromArgb(20, 222, 210, 198),       // rgba(222,210,198,.08)
            Text = neutral100,
            TextStrong = neutral50,
            TextMuted = neutral300,
            TextDim = neutral400,
            Accent = copper500,
            AccentText = copper500,
            AccentHover = copper400,
            AccentSoft = Color.FromArgb(31, 184, 115, 51),     // rgba(184,115,51,.12)
            OnAccent = neutral950,
            Highlight = gold500,
            HighlightSoft = Color.FromArgb(36, 212, 160, 23),  // rgba(212,160,23,.14)
            // Gold is both the readout and the selection signal in the HUD.
            Select = gold500,
            SelectSoft = Color.FromArgb(36, 212, 160, 23),
            SelectRing = Color.FromArgb(127, 212, 160, 23),
            Progress = gold500,
            Eyebrow = neutral300,
            StatusOk = chartreuse500,
            StatusPending = gold300,
            StatusIdle = silver600,
            StatusWarning = ember500,
            StatusDanger = red500,
            DangerText = red400,
            DangerFill = Color.FromArgb(56, 139, 0, 0),        // rgba(139,0,0,.22)
            DangerBorder = Color.FromArgb(115, 220, 20, 60),   // rgba(220,20,60,.45)
            Info = arc400,
            FocusRing = gold400,
            // Input fill from docs/04-components.md: rgba(255,255,255,0.04).
            InputFill = Color.FromArgb(10, 255, 255, 255),
            // --color-bg at 80% (Proposed, native mapping).
            Scrim = Color.FromArgb(204, 6, 4, 4),
            NeutralData = silver400,

            Body = mono,
            Sans = sans,
            Mono = mono,
            Label = display,
            Heading = displayBold,
            Button = displayBold,
            Number = mono,
            BigNumber = monoLight,
            Text2xs = 10, TextXs = 11, TextSm = 12, TextMd = 13, TextLg = 15, TextXl = 18, Text2xl = 24, Text3xl = 32,
            TextTitle = 11,
            TrackingLabel = 0.18f,
            TrackingWide = 0.08f,
            Uppercase = true,

            // The guide allows 2px/4px, but the app has always drawn square: keep it.
            RadiusSm = 0, RadiusMd = 0, RadiusLg = 0, RadiusXl = 0,
            Pills = false,
            ControlHeight = 28,
            PrimaryHeight = 32,
            DurationFast = 120,
            DurationNormal = 220,
            LoopPulse = 1600,
            Hud = true,
        };
    }
}
