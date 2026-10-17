namespace PhoneExplorer.Hud.Themes;

/// <summary>
/// Pastel Girl: soft, round and very cute, pastels glowing on a plum-black field (the dark
/// variant). Mirrored by hand from F:\Projects\StyleGuides\PastelGirl\tokens\tokens.css (copied
/// 2026-10-17). When the guide changes, re-copy the values here.
/// </summary>
public static class PastelGirl
{
    public static ThemeDef Create()
    {
        // Palette (only what the semantic layer needs)
        var plum950 = ThemeDef.Hex(0x120b12);
        var plum900 = ThemeDef.Hex(0x1a111b);
        var plum400 = ThemeDef.Hex(0xa08aa3);
        var plum300 = ThemeDef.Hex(0xc3adc0);
        var cream50 = ThemeDef.Hex(0xfff8f0);
        var cream100 = ThemeDef.Hex(0xfbefe3);
        var cream200 = ThemeDef.Hex(0xf3e1cf);
        var pink500 = ThemeDef.Hex(0xf5a3c3);
        var pink400 = ThemeDef.Hex(0xf8bcd3);
        var pink300 = ThemeDef.Hex(0xfbd3e2);
        var mint500 = ThemeDef.Hex(0xa6e7c8);
        var sky500 = ThemeDef.Hex(0xa8d4f5);
        var butter500 = ThemeDef.Hex(0xf6e19c);
        var peach500 = ThemeDef.Hex(0xf9bf9e);
        var silver500 = ThemeDef.Hex(0xb9b4c2);
        var cherry500 = ThemeDef.Hex(0xff5247);
        var cherry400 = ThemeDef.Hex(0xff6a5e);

        // --font-sans Nunito, --font-display Fredoka, --font-mono Cascadia Mono (docs/02-typography.md),
        // falling back to Segoe UI Variable as the guide allows. Weights: 400 body, 600 titles and
        // numbers, 700 buttons and eyebrows.
        var sans = ThemeDef.Pick(new("Nunito"), new("Segoe UI Variable Text"), new("Segoe UI"));
        var sansBold = ThemeDef.Pick(new("Nunito", FontStyle.Bold), new("Segoe UI Variable Text", FontStyle.Bold), new("Segoe UI", FontStyle.Bold));
        var display = ThemeDef.Pick(new("Fredoka SemiBold"), new("Fredoka", FontStyle.Bold), new("Segoe UI Variable Display Semib"), new("Segoe UI Semibold"));
        var mono = ThemeDef.Pick(new("Cascadia Mono"), new("Cascadia Code"), new("Consolas"));

        return new ThemeDef
        {
            Id = "PastelGirl",
            DisplayName = "Pastel Girl",

            Bg = plum950,
            BgElevated = plum900,
            Surface = ThemeDef.Rgba(44, 31, 47, 0.62f),
            SurfaceHover = ThemeDef.Rgba(52, 37, 56, 0.72f),
            SurfaceRaised = ThemeDef.Rgba(34, 23, 36, 0.92f),
            Border = ThemeDef.Rgba(245, 163, 195, 0.16f),
            BorderStrong = ThemeDef.Rgba(245, 163, 195, 0.42f),
            Divider = ThemeDef.Rgba(251, 239, 227, 0.07f),
            Text = cream100,
            TextStrong = cream50,
            TextMuted = plum300,
            TextDim = plum400,
            Accent = pink500,
            AccentText = pink500,
            AccentHover = pink400,
            AccentSoft = ThemeDef.Rgba(245, 163, 195, 0.14f),
            OnAccent = plum950,
            // Numbers are cream; pink is selection (docs/01-color.md).
            Highlight = cream200,
            HighlightSoft = ThemeDef.Rgba(243, 225, 207, 0.10f),
            Select = pink500,
            SelectSoft = ThemeDef.Rgba(245, 163, 195, 0.22f),   // --color-accent-soft-hover
            SelectRing = pink500,
            Progress = pink500,
            // .soft-eyebrow: small, bold, pink.
            Eyebrow = pink500,
            StatusOk = mint500,
            StatusPending = butter500,
            StatusIdle = plum400,
            StatusWarning = peach500,
            StatusDanger = cherry500,
            DangerText = cherry400,
            DangerFill = ThemeDef.Rgba(196, 26, 18, 0.20f),
            DangerBorder = ThemeDef.Rgba(255, 82, 71, 0.55f),
            Info = sky500,
            FocusRing = pink300,
            InputFill = ThemeDef.Rgba(18, 11, 18, 0.55f),         // --color-surface-sunken
            Scrim = ThemeDef.Rgba(18, 11, 18, 0.80f),
            NeutralData = silver500,                              // silver: the hardware detail

            Body = sans,
            Sans = sans,
            Mono = mono,
            Label = sansBold,
            Heading = display,
            Button = sansBold,
            Number = display,
            BigNumber = display,
            // The guide's scale starts at 12px; the HUD's 10px step maps to it too.
            Text2xs = 12, TextXs = 12, TextSm = 13, TextMd = 15, TextLg = 17, TextXl = 20, Text2xl = 26, Text3xl = 36,
            TextTitle = 20,
            TrackingLabel = 0.06f,   // --tracking-caps, still sentence case
            TrackingWide = 0f,
            Uppercase = false,

            RadiusSm = 8, RadiusMd = 14, RadiusLg = 20, RadiusXl = 28,
            Pills = true,
            ControlHeight = 32,
            PrimaryHeight = 36,
            DurationFast = 160,
            DurationNormal = 260,
            LoopPulse = 1800,
            Hud = false,
        };
    }
}
