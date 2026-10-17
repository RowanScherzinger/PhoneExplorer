using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PhoneExplorer.Hud;

/// <summary>
/// Jarvis Apps design tokens for a native WinForms app.
///
/// Mirrored by hand from F:\Projects\StyleGuides\JarvisApps\tokens\tokens.css (copied 2026-10-17;
/// see decisions/0008 in the guide). Each member is named after its token: ColorAccent is
/// --color-accent. When the guide changes, re-copy the values here. Nothing outside this file
/// may use a literal colour, font, size or duration.
///
/// Native controls cannot draw translucent backgrounds, so rgba tokens used as a control
/// BackColor are pre-blended over --color-bg (the *Solid members). Glass is tint only, no blur.
/// </summary>
public static class Theme
{
    // ---- Palette (only what the semantic layer needs) ------------------------------------

    static readonly Color PaletteNeutral950 = Hex(0x060404);
    static readonly Color PaletteNeutral900 = Hex(0x0e0a09);
    static readonly Color PaletteNeutral400 = Hex(0x85796f);
    static readonly Color PaletteNeutral300 = Hex(0xada197);
    static readonly Color PaletteNeutral100 = Hex(0xded2c6);
    static readonly Color PaletteNeutral50 = Hex(0xf2eae1);
    static readonly Color PaletteCopper500 = Hex(0xb87333);
    static readonly Color PaletteCopper400 = Hex(0xcf8c4e);
    static readonly Color PaletteGold500 = Hex(0xd4a017);
    static readonly Color PaletteGold400 = Hex(0xe6b93e);
    static readonly Color PaletteGold300 = Hex(0xf2d27a);
    static readonly Color PaletteSilver600 = Hex(0x8c8c8c);
    static readonly Color PaletteSilver400 = Hex(0xc0c0c0);
    static readonly Color PaletteRed500 = Hex(0xdc143c);
    static readonly Color PaletteRed400 = Hex(0xf0485f);
    static readonly Color PaletteEmber500 = Hex(0xf07a2a);
    static readonly Color PaletteChartreuse500 = Hex(0xa3d21f);
    static readonly Color PaletteArc400 = Hex(0x6ec6ff);

    // ---- Semantic colour -----------------------------------------------------------------

    public static readonly Color ColorBg = PaletteNeutral950;
    public static readonly Color ColorBgElevated = PaletteNeutral900;
    public static readonly Color ColorSurface = Color.FromArgb(158, 14, 10, 9);          // rgba(14,10,9,.62)
    public static readonly Color ColorSurfaceHover = Color.FromArgb(191, 21, 16, 14);    // rgba(21,16,14,.75)
    public static readonly Color ColorSurfaceRaised = Color.FromArgb(224, 21, 16, 14);   // rgba(21,16,14,.88)

    public static readonly Color ColorBorder = Color.FromArgb(56, 184, 115, 51);         // rgba(184,115,51,.22)
    public static readonly Color ColorBorderStrong = Color.FromArgb(128, 184, 115, 51);  // rgba(184,115,51,.50)
    public static readonly Color ColorDivider = Color.FromArgb(20, 222, 210, 198);       // rgba(222,210,198,.08)

    public static readonly Color ColorText = PaletteNeutral100;
    public static readonly Color ColorTextStrong = PaletteNeutral50;
    public static readonly Color ColorTextMuted = PaletteNeutral300;
    public static readonly Color ColorTextDim = PaletteNeutral400;

    public static readonly Color ColorAccent = PaletteCopper500;
    public static readonly Color ColorAccentHover = PaletteCopper400;
    public static readonly Color ColorAccentSoft = Color.FromArgb(31, 184, 115, 51);     // rgba(184,115,51,.12)
    public static readonly Color ColorOnAccent = PaletteNeutral950;

    public static readonly Color ColorHighlight = PaletteGold500;
    public static readonly Color ColorHighlightSoft = Color.FromArgb(36, 212, 160, 23);  // rgba(212,160,23,.14)

    public static readonly Color ColorStatusOk = PaletteChartreuse500;
    public static readonly Color ColorStatusPending = PaletteGold300;
    public static readonly Color ColorStatusIdle = PaletteSilver600;
    public static readonly Color ColorStatusWarning = PaletteEmber500;
    public static readonly Color ColorStatusDanger = PaletteRed500;
    public static readonly Color ColorDangerText = PaletteRed400;
    public static readonly Color ColorDangerFill = Color.FromArgb(56, 139, 0, 0);        // rgba(139,0,0,.22)
    public static readonly Color ColorDangerBorder = Color.FromArgb(115, 220, 20, 60);   // rgba(220,20,60,.45)
    public static readonly Color ColorInfo = PaletteArc400;

    public static readonly Color ColorFocusRing = PaletteGold400;
    public static readonly Color ColorSelection = Color.FromArgb(77, 212, 160, 23);      // rgba(212,160,23,.30)

    /// <summary>Input fill from docs/04-components.md: rgba(255,255,255,0.04).</summary>
    public static readonly Color ColorInputFill = Color.FromArgb(10, 255, 255, 255);
    /// <summary>Chip behind badges drawn over photos: --color-bg at 80% (Proposed, native mapping).</summary>
    public static readonly Color ColorScrim = Color.FromArgb(204, 6, 4, 4);
    /// <summary>Silver for neutral data marks (badges for edited / live).</summary>
    public static readonly Color ColorNeutralData = PaletteSilver400;

    // Opaque equivalents for native control backgrounds.
    public static readonly Color SolidSurface = Over(ColorSurface, ColorBg);
    public static readonly Color SolidSurfaceRaised = Over(ColorSurfaceRaised, ColorBg);
    public static readonly Color SolidInput = Over(ColorInputFill, ColorBgElevated);
    public static readonly Color SolidBorder = Over(ColorBorder, ColorBg);
    public static readonly Color SolidBorderStrong = Over(ColorBorderStrong, ColorBg);

    // ---- Type ----------------------------------------------------------------------------

    // --font-mono / --font-sans / --font-display, resolved to the first installed family.
    public static readonly string FontMono = FirstInstalled("Cascadia Mono", "Cascadia Code", "DM Mono", "Consolas");
    public static readonly string FontMonoLight = FirstInstalled("Cascadia Mono Light", FontMono);
    public static readonly string FontSans = FirstInstalled("Inter", "Segoe UI Variable Text", "Segoe UI");
    public static readonly string FontDisplay = FirstInstalled("Orbitron", "Bahnschrift");
    public static readonly string FontDisplayBold = FontDisplay == "Bahnschrift" ? FirstInstalled("Bahnschrift SemiBold", "Bahnschrift") : FontDisplay;

    public const float Text2xs = 10, TextXs = 11, TextSm = 12, TextMd = 13, TextLg = 15, TextXl = 18, Text2xl = 24, Text3xl = 32;
    public const float TrackingLabel = 0.18f, TrackingWide = 0.08f;

    // ---- Space, shape, line, motion ------------------------------------------------------

    public const int Space1 = 4, Space2 = 8, Space3 = 12, Space4 = 16, Space5 = 20, Space6 = 24, Space8 = 32, Space10 = 40, Space12 = 48;
    public const int RadiusSm = 2, RadiusMd = 4;
    public const float LineHair = 1f, LineThin = 1.5f, LineBold = 2f;
    public const int BracketSize = 10;
    public const float BracketRestOpacity = 0.45f;
    public const int DurationFast = 120, DurationNormal = 220, DurationSlow = 400, DurationBoot = 600;
    public const int LoopPulse = 1600, LoopScan = 5000;
    public const float DisabledOpacity = 0.40f;
    public const int ControlHeight = 28, PrimaryHeight = 32;

    // ---- Helpers -------------------------------------------------------------------------

    static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);

    /// <summary>Composites a translucent colour over an opaque one.</summary>
    public static Color Over(Color top, Color under)
    {
        float a = top.A / 255f;
        return Color.FromArgb(255,
            (int)(top.R * a + under.R * (1 - a)),
            (int)(top.G * a + under.G * (1 - a)),
            (int)(top.B * a + under.B * (1 - a)));
    }

    /// <summary>Scales a colour's opacity (for disabled states and fades in owner-drawn code).</summary>
    public static Color Alpha(Color c, float opacity) => Color.FromArgb((int)Math.Clamp(c.A * opacity, 0, 255), c);

    static string FirstInstalled(params string[] families)
    {
        using var installed = new System.Drawing.Text.InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return families.FirstOrDefault(names.Contains) ?? families[^1];
    }

    static readonly Dictionary<(string, float, FontStyle), Font> fonts = new();

    /// <summary>A cached font. Size is in CSS px at 96 dpi; it is converted to points so it scales with DPI.</summary>
    public static Font Font(string family, float px, FontStyle style = FontStyle.Regular)
    {
        var key = (family, px, style);
        if (!fonts.TryGetValue(key, out var f))
            fonts[key] = f = new Font(family, px * 0.75f, style, GraphicsUnit.Point);
        return f;
    }

    public static Font Mono(float px = TextMd) => Font(FontMono, px);
    public static Font Sans(float px = TextMd) => Font(FontSans, px);
    public static Font Label(float px = Text2xs) => Font(FontDisplay, px);
    public static Font Heading(float px = TextXs) => Font(FontDisplayBold, px);

    /// <summary>
    /// Text with letter-spacing (GDI has none, so characters are placed one by one).
    /// Used for uppercase labels, headings and button text. Returns the width drawn.
    /// </summary>
    public static int DrawTracked(Graphics g, string text, Font font, Color color, Point origin, float trackingEm)
    {
        float x = origin.X, spacing = font.SizeInPoints / 0.75f * trackingEm * g.DpiX / 96f;
        foreach (var ch in text)
        {
            var s = ch.ToString();
            TextRenderer.DrawText(g, s, font, new Point((int)Math.Round(x), origin.Y), color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            x += CharWidth(g, font, ch) + spacing;
        }
        return (int)Math.Round(x - origin.X - (text.Length > 0 ? spacing : 0));
    }

    public static Size MeasureTracked(Graphics? g, string text, Font font, float trackingEm)
    {
        float dpi = g?.DpiX ?? 96f;
        float spacing = font.SizeInPoints / 0.75f * trackingEm * dpi / 96f;
        float w = text.Sum(ch => (float)CharWidth(g, font, ch)) + spacing * Math.Max(0, text.Length - 1);
        return new Size((int)Math.Ceiling(w), TextRenderer.MeasureText("X", font, Size.Empty, TextFormatFlags.NoPadding).Height);
    }

    static readonly Dictionary<(Font, char), int> charWidths = new();

    static int CharWidth(IDeviceContext? dc, Font font, char ch)
    {
        if (charWidths.TryGetValue((font, ch), out var w)) return w;
        w = ch == ' '
            ? TextRenderer.MeasureText("x x", font, Size.Empty, TextFormatFlags.NoPadding).Width - TextRenderer.MeasureText("xx", font, Size.Empty, TextFormatFlags.NoPadding).Width
            : TextRenderer.MeasureText(ch.ToString(), font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        charWidths[(font, ch)] = w;
        return w;
    }

    /// <summary>Corner brackets (.hud-corner): four L-shapes on the rectangle's corners.</summary>
    public static void DrawBrackets(Graphics g, Rectangle r, Color color, int size, float width)
    {
        using var pen = new Pen(color, width) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter };
        int l = r.Left, t = r.Top, rt = r.Right - 1, b = r.Bottom - 1;
        g.DrawLines(pen, new[] { new Point(l, t + size), new Point(l, t), new Point(l + size, t) });
        g.DrawLines(pen, new[] { new Point(rt - size, t), new Point(rt, t), new Point(rt, t + size) });
        g.DrawLines(pen, new[] { new Point(l, b - size), new Point(l, b), new Point(l + size, b) });
        g.DrawLines(pen, new[] { new Point(rt - size, b), new Point(rt, b), new Point(rt, b - size) });
    }

    /// <summary>The focus-visible ring: 1.5px gold outline, 2px outside the element.</summary>
    public static void DrawFocusRing(Graphics g, Rectangle r, int offset)
    {
        var f = Rectangle.Inflate(r, offset, offset);
        using var pen = new Pen(ColorFocusRing, LineThin * g.DpiX / 96f);
        g.DrawRectangle(pen, f.X, f.Y, f.Width - 1, f.Height - 1);
    }

    public enum Shape { DiamondFilled, DiamondHollow, SquareHollow, Triangle, SquareFilled, Circle }

    /// <summary>Status shapes from docs/01-color.md, drawn as polygons (never emoji or font glyphs).</summary>
    public static void DrawShape(Graphics g, Shape shape, Rectangle r, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, LineThin * g.DpiX / 96f);
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, h = r.Width / 2f;
        var diamond = new[] { new PointF(cx, cy - h), new PointF(cx + h, cy), new PointF(cx, cy + h), new PointF(cx - h, cy) };
        switch (shape)
        {
            case Shape.DiamondFilled: g.FillPolygon(brush, diamond); break;
            case Shape.DiamondHollow: g.DrawPolygon(pen, diamond); break;
            case Shape.SquareHollow: g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2); break;
            case Shape.SquareFilled: g.FillRectangle(brush, r); break;
            case Shape.Triangle: g.FillPolygon(brush, new[] { new PointF(cx, r.Y), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) }); break;
            case Shape.Circle: g.FillEllipse(brush, r); break;
        }
        g.SmoothingMode = old;
    }

    // ---- Reduced motion ------------------------------------------------------------------

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint action, uint param, out bool value, uint winIni);

    /// <summary>
    /// The native equivalent of prefers-reduced-motion: Windows Settings › Accessibility ›
    /// Visual effects › Animation effects. When off, durations are 0 and loops stop.
    /// </summary>
    public static bool ReducedMotion => !(SystemParametersInfo(0x1042 /* SPI_GETCLIENTAREAANIMATION */, 0, out var on, 0) && on);

    /// <summary>The house curve --ease-out-expo, cubic-bezier(0.16, 1, 0.3, 1), approximated.</summary>
    public static float EaseOutExpo(float t) => t >= 1 ? 1 : 1 - MathF.Pow(2, -10 * t);

    /// <summary>The .hud-boot flicker keyframes: opacity at progress t (0..1).</summary>
    public static float BootOpacity(float t) => t switch
    {
        < 0.05f => t / 0.05f * 0.8f,
        < 0.06f => 0.2f,
        < 0.08f => 0.9f,
        < 0.10f => 0.4f,
        < 0.12f => 1f,
        _ => 1f,
    };
}
