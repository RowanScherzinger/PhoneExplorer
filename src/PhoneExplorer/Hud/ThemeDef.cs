namespace PhoneExplorer.Hud;

/// <summary>A font family plus the style that gives the weight a guide asks for.</summary>
public readonly record struct Face(string Family, FontStyle Style = FontStyle.Regular);

/// <summary>
/// One style guide's tokens, mirrored by hand for a native app. Every theme fills every member;
/// code reads them through <see cref="Theme"/>. Colours are named after the guide's semantic
/// tokens (Accent is --color-accent); a few are app roles that guides map differently
/// (Select, Progress, Number), documented where each theme sets them.
/// </summary>
public sealed class ThemeDef
{
    /// <summary>Settings key, never shown.</summary>
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>The window title bar and native controls follow this.</summary>
    public bool Dark { get; init; } = true;

    // ---- Colour --------------------------------------------------------------------------

    public required Color Bg { get; init; }
    public required Color BgElevated { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceHover { get; init; }
    public required Color SurfaceRaised { get; init; }
    public required Color Border { get; init; }
    public required Color BorderStrong { get; init; }
    public required Color Divider { get; init; }
    public required Color Text { get; init; }
    public required Color TextStrong { get; init; }
    public required Color TextMuted { get; init; }
    public required Color TextDim { get; init; }
    public required Color Accent { get; init; }
    /// <summary>Accent-coloured words and icons (can differ from the fill colour).</summary>
    public required Color AccentText { get; init; }
    public required Color AccentHover { get; init; }
    public required Color AccentSoft { get; init; }
    public required Color OnAccent { get; init; }
    /// <summary>Readout numbers.</summary>
    public required Color Highlight { get; init; }
    public required Color HighlightSoft { get; init; }
    /// <summary>Selected rows, tiles and check boxes.</summary>
    public required Color Select { get; init; }
    public required Color SelectSoft { get; init; }
    /// <summary>The outline of a selected tile.</summary>
    public required Color SelectRing { get; init; }
    /// <summary>Filled part of progress bars.</summary>
    public required Color Progress { get; init; }
    /// <summary>Small section labels ("Show", "Library").</summary>
    public required Color Eyebrow { get; init; }
    public required Color StatusOk { get; init; }
    public required Color StatusPending { get; init; }
    public required Color StatusIdle { get; init; }
    public required Color StatusWarning { get; init; }
    public required Color StatusDanger { get; init; }
    public required Color DangerText { get; init; }
    public required Color DangerFill { get; init; }
    public required Color DangerBorder { get; init; }
    public required Color Info { get; init; }
    public required Color FocusRing { get; init; }
    public required Color InputFill { get; init; }
    /// <summary>Chip behind badges drawn over photos.</summary>
    public required Color Scrim { get; init; }
    /// <summary>Neutral data marks (badges for edited / live).</summary>
    public required Color NeutralData { get; init; }

    // ---- Type ----------------------------------------------------------------------------

    public required Face Body { get; init; }
    public required Face Sans { get; init; }
    public required Face Mono { get; init; }
    public required Face Label { get; init; }
    public required Face Heading { get; init; }
    public required Face Button { get; init; }
    public required Face Number { get; init; }
    public required Face BigNumber { get; init; }

    public required float Text2xs { get; init; }
    public required float TextXs { get; init; }
    public required float TextSm { get; init; }
    public required float TextMd { get; init; }
    public required float TextLg { get; init; }
    public required float TextXl { get; init; }
    public required float Text2xl { get; init; }
    public required float Text3xl { get; init; }
    /// <summary>Dialog titles.</summary>
    public required float TextTitle { get; init; }
    public required float TrackingLabel { get; init; }
    public required float TrackingWide { get; init; }
    /// <summary>Labels, headings and buttons in capitals (HUD), or as written (sentence case).</summary>
    public required bool Uppercase { get; init; }

    // ---- Shape, size, motion -------------------------------------------------------------

    public required int RadiusSm { get; init; }
    public required int RadiusMd { get; init; }
    public required int RadiusLg { get; init; }
    public required int RadiusXl { get; init; }
    /// <summary>Buttons, chips and badges are fully round.</summary>
    public required bool Pills { get; init; }
    public required int ControlHeight { get; init; }
    public required int PrimaryHeight { get; init; }
    public required int DurationFast { get; init; }
    public required int DurationNormal { get; init; }
    public required int LoopPulse { get; init; }

    /// <summary>
    /// The HUD details: corner brackets, edge bars on selected rows, the boot flicker, segmented
    /// progress and square status shapes. Off means soft: hearts and moons, sparkles, dots.
    /// </summary>
    public required bool Hud { get; init; }

    // ---- Derived -------------------------------------------------------------------------

    // Native controls cannot draw translucent backgrounds: these are pre-blended over Bg.
    public Color SolidSurface => Theme.Over(Surface, Bg);
    public Color SolidSurfaceRaised => Theme.Over(SurfaceRaised, Bg);
    public Color SolidBorder => Theme.Over(Border, Bg);
    public Color SolidBorderStrong => Theme.Over(BorderStrong, Bg);

    // ---- Helpers for the theme files -----------------------------------------------------

    internal static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
    internal static Color Rgba(int r, int g, int b, float a) => Color.FromArgb((int)Math.Round(a * 255), r, g, b);

    static HashSet<string>? installed;

    /// <summary>The first face whose family is installed, else the last one.</summary>
    internal static Face Pick(params Face[] faces)
    {
        if (installed == null)
        {
            using var fonts = new System.Drawing.Text.InstalledFontCollection();
            installed = fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        return faces.FirstOrDefault(f => installed.Contains(f.Family), faces[^1]);
    }
}
