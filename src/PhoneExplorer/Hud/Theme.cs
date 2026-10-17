using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using PhoneExplorer.Hud.Themes;

namespace PhoneExplorer.Hud;

/// <summary>
/// Design tokens for a native WinForms app, from whichever style guide is in use.
///
/// The values live in Hud/Themes (one file per guide, each mirrored by hand from the guide's
/// tokens.css); this class reads them from <see cref="Current"/>, so members are named after
/// their token (ColorAccent is --color-accent). Nothing outside these files may use a literal
/// colour, font, size or duration. Paint code reads tokens on every paint, so a theme swap only
/// needs controls that cache a value (fonts, sizes, BackColor) to be re-applied: see
/// MainForm.ApplyTheme.
///
/// Native controls cannot draw translucent backgrounds, so rgba tokens used as a control
/// BackColor are pre-blended over --color-bg (the *Solid members). Glass is tint only, no blur.
/// </summary>
public static class Theme
{
    public static readonly IReadOnlyList<ThemeDef> All = new[] { JarvisApps.Create(), PastelGirl.Create() };

    public static ThemeDef Current { get; private set; } = All[0];

    /// <summary>Switches theme (unknown ids fall back to the first). Returns true if it changed.</summary>
    public static bool Use(string? id)
    {
        var next = All.FirstOrDefault(t => t.Id == id) ?? All[0];
        if (next == Current) return false;
        Current = next;
        return true;
    }

    // ---- Semantic colour -----------------------------------------------------------------

    public static Color ColorBg => Current.Bg;
    public static Color ColorBgElevated => Current.BgElevated;
    public static Color ColorSurface => Current.Surface;
    public static Color ColorSurfaceHover => Current.SurfaceHover;
    public static Color ColorSurfaceRaised => Current.SurfaceRaised;
    public static Color ColorBorder => Current.Border;
    public static Color ColorBorderStrong => Current.BorderStrong;
    public static Color ColorDivider => Current.Divider;
    public static Color ColorText => Current.Text;
    public static Color ColorTextStrong => Current.TextStrong;
    public static Color ColorTextMuted => Current.TextMuted;
    public static Color ColorTextDim => Current.TextDim;
    public static Color ColorAccent => Current.Accent;
    public static Color ColorAccentText => Current.AccentText;
    public static Color ColorAccentHover => Current.AccentHover;
    public static Color ColorAccentSoft => Current.AccentSoft;
    public static Color ColorOnAccent => Current.OnAccent;
    public static Color ColorHighlight => Current.Highlight;
    public static Color ColorHighlightSoft => Current.HighlightSoft;
    public static Color ColorSelect => Current.Select;
    public static Color ColorSelectSoft => Current.SelectSoft;
    public static Color ColorSelectRing => Current.SelectRing;
    public static Color ColorProgress => Current.Progress;
    public static Color ColorEyebrow => Current.Eyebrow;
    public static Color ColorStatusOk => Current.StatusOk;
    public static Color ColorStatusPending => Current.StatusPending;
    public static Color ColorStatusIdle => Current.StatusIdle;
    public static Color ColorStatusWarning => Current.StatusWarning;
    public static Color ColorStatusDanger => Current.StatusDanger;
    public static Color ColorDangerText => Current.DangerText;
    public static Color ColorDangerFill => Current.DangerFill;
    public static Color ColorDangerBorder => Current.DangerBorder;
    public static Color ColorInfo => Current.Info;
    public static Color ColorFocusRing => Current.FocusRing;
    public static Color ColorInputFill => Current.InputFill;
    public static Color ColorScrim => Current.Scrim;
    public static Color ColorNeutralData => Current.NeutralData;

    public static Color SolidSurface => Current.SolidSurface;
    public static Color SolidSurfaceRaised => Current.SolidSurfaceRaised;
    public static Color SolidBorder => Current.SolidBorder;
    public static Color SolidBorderStrong => Current.SolidBorderStrong;

    // ---- Type ----------------------------------------------------------------------------

    public static float Text2xs => Current.Text2xs;
    public static float TextXs => Current.TextXs;
    public static float TextSm => Current.TextSm;
    public static float TextMd => Current.TextMd;
    public static float TextLg => Current.TextLg;
    public static float TextXl => Current.TextXl;
    public static float Text2xl => Current.Text2xl;
    public static float Text3xl => Current.Text3xl;
    public static float TextTitle => Current.TextTitle;
    public static float TrackingLabel => Current.TrackingLabel;
    public static float TrackingWide => Current.TrackingWide;

    // ---- Space, shape, line, motion ------------------------------------------------------

    // Both guides share the 4px spacing scale and line weights.
    public const int Space1 = 4, Space2 = 8, Space3 = 12, Space4 = 16, Space5 = 20, Space6 = 24, Space8 = 32, Space10 = 40, Space12 = 48;
    public const float LineHair = 1f, LineThin = 1.5f, LineBold = 2f;
    public const int BracketSize = 10;
    public const float BracketRestOpacity = 0.45f;
    public const int DurationBoot = 600;
    public const float DisabledOpacity = 0.40f;
    public static int DurationFast => Current.DurationFast;
    public static int DurationNormal => Current.DurationNormal;
    public static int LoopPulse => Current.LoopPulse;
    public static int ControlHeight => Current.ControlHeight;
    public static int PrimaryHeight => Current.PrimaryHeight;
    /// <summary>HUD details on (brackets, edge bars, boot flicker) or the soft look.</summary>
    public static bool Hud => Current.Hud;

    // ---- Helpers -------------------------------------------------------------------------

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

    /// <summary>Label, heading and button text as the theme writes it: capitals, or as given (sentence case).</summary>
    public static string Case(string text) => Current.Uppercase ? text.ToUpperInvariant() : text;

    /// <summary>Copy for moments the soft theme makes cute (success, empty states); plain words otherwise.</summary>
    public static string Say(string plain, string cute) => Current.Hud ? plain : cute;

    static readonly Dictionary<(string, float, FontStyle), Font> fonts = new();

    /// <summary>A cached font. Size is in CSS px at 96 dpi; it is converted to points so it scales with DPI.</summary>
    public static Font Font(Face face, float px)
    {
        var key = (face.Family, px, face.Style);
        if (!fonts.TryGetValue(key, out var f))
            fonts[key] = f = new Font(face.Family, px * 0.75f, face.Style, GraphicsUnit.Point);
        return f;
    }

    /// <summary>Running text: mono in the HUD, the sans face in soft themes.</summary>
    public static Font Body(float? px = null) => Font(Current.Body, px ?? TextMd);
    /// <summary>Longer prose (dialog messages, empty states).</summary>
    public static Font Sans(float? px = null) => Font(Current.Sans, px ?? TextMd);
    public static Font Mono(float? px = null) => Font(Current.Mono, px ?? TextMd);
    /// <summary>Small section labels and units.</summary>
    public static Font Label(float? px = null) => Font(Current.Label, px ?? Text2xs);
    public static Font Heading(float? px = null) => Font(Current.Heading, px ?? TextXs);
    public static Font ButtonFont(float? px = null) => Font(Current.Button, px ?? TextXs);
    /// <summary>Tabular readout values.</summary>
    public static Font Number(float? px = null) => Font(Current.Number, px ?? TextMd);
    /// <summary>The one big readout per panel.</summary>
    public static Font BigNumber(float px) => Font(Current.BigNumber, px);

    /// <summary>
    /// Text with letter-spacing (GDI has none, so characters are placed one by one).
    /// Used for labels, headings and button text. Returns the width drawn.
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

    // ---- Shapes --------------------------------------------------------------------------

    /// <summary>Corner sizes from the guides' shape tables. Pill is fully round where the theme uses pills.</summary>
    public enum Corner { Sm, Md, Lg, Xl, Pill }

    /// <summary>Corner radius in device pixels for a box of this size (0 = square).</summary>
    public static int Radius(Graphics g, Corner corner, Rectangle r)
    {
        int logical = corner switch
        {
            Corner.Sm => Current.RadiusSm,
            Corner.Md => Current.RadiusMd,
            Corner.Lg => Current.RadiusLg,
            Corner.Xl => Current.RadiusXl,
            _ => Current.Pills ? int.MaxValue : Current.RadiusSm,
        };
        if (logical == 0) return 0;
        int px = logical == int.MaxValue ? int.MaxValue : (int)(logical * g.DpiX / 96f);
        return Math.Min(px, Math.Min(r.Width, r.Height) / 2);
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Fills a box with the theme's corner for it (square in the HUD).</summary>
    public static void Fill(Graphics g, Color color, Rectangle r, Corner corner)
    {
        using var brush = new SolidBrush(color);
        int radius = Radius(g, corner, r);
        if (radius == 0) { g.FillRectangle(brush, r); return; }
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundRect(r, radius);
        g.FillPath(brush, path);
        g.SmoothingMode = old;
    }

    /// <summary>Outlines a box just inside its edge, with the theme's corner for it.</summary>
    public static void Stroke(Graphics g, Color color, Rectangle r, Corner corner, float width = LineHair)
    {
        int radius = Radius(g, corner, r);
        if (radius == 0)
        {
            using var square = new Pen(color, width);
            g.DrawRectangle(square, r.X, r.Y, r.Width - 1, r.Height - 1);
            return;
        }
        float w = width * g.DpiX / 96f;
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, w);
        using var path = RoundRect(new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w), radius - w / 2);
        g.DrawPath(pen, path);
        g.SmoothingMode = old;
    }

    /// <summary>Corner brackets (.hud-corner): four L-shapes on the rectangle's corners. HUD themes only.</summary>
    public static void DrawBrackets(Graphics g, Rectangle r, Color color, int size, float width)
    {
        if (!Hud) return;
        using var pen = new Pen(color, width) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter };
        int l = r.Left, t = r.Top, rt = r.Right - 1, b = r.Bottom - 1;
        g.DrawLines(pen, new[] { new Point(l, t + size), new Point(l, t), new Point(l + size, t) });
        g.DrawLines(pen, new[] { new Point(rt - size, t), new Point(rt, t), new Point(rt, t + size) });
        g.DrawLines(pen, new[] { new Point(l, b - size), new Point(l, b), new Point(l + size, b) });
        g.DrawLines(pen, new[] { new Point(rt - size, b), new Point(rt, b), new Point(rt, b - size) });
    }

    /// <summary>The focus-visible ring: 1.5px outline, offset outside the element.</summary>
    public static void DrawFocusRing(Graphics g, Rectangle r, int offset, Corner corner = Corner.Sm) =>
        Stroke(g, ColorFocusRing, Rectangle.Inflate(r, offset, offset), corner, LineThin);

    public enum Status { Ok, Pending, Idle, Warning, Danger, Info }

    /// <summary>
    /// Status glyphs, always shown with words. HUD (docs/01-color.md): filled diamond, hollow
    /// diamond, hollow square, triangle, filled square, circle. Soft (decisions/0010): filled heart,
    /// crescent moon, hollow heart, triangle, broken heart, dot. Drawn as paths, never emoji or fonts.
    /// </summary>
    public static void DrawStatus(Graphics g, Status status, Rectangle r, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, LineThin * g.DpiX / 96f);
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, h = r.Width / 2f;
        var diamond = new[] { new PointF(cx, cy - h), new PointF(cx + h, cy), new PointF(cx, cy + h), new PointF(cx - h, cy) };
        var triangle = new[] { new PointF(cx, r.Y), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) };
        if (Hud)
            switch (status)
            {
                case Status.Ok: g.FillPolygon(brush, diamond); break;
                case Status.Pending: g.DrawPolygon(pen, diamond); break;
                case Status.Idle: g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2); break;
                case Status.Danger: g.FillRectangle(brush, r); break;
                case Status.Warning: g.FillPolygon(brush, triangle); break;
                case Status.Info: g.FillEllipse(brush, r); break;
            }
        else
            switch (status)
            {
                case Status.Ok: { using var heart = Heart(r); g.FillPath(brush, heart); break; }
                case Status.Idle: { var inset = RectangleF.Inflate(r, -pen.Width / 2, -pen.Width / 2); using var heart = Heart(inset); g.DrawPath(pen, heart); break; }
                case Status.Pending: { using var moon = Moon(r); g.FillPath(brush, moon); break; }
                case Status.Danger: BrokenHeart(g, brush, r); break;
                case Status.Warning: { pen.LineJoin = LineJoin.Round; g.FillPolygon(brush, triangle); g.DrawPolygon(pen, triangle); break; }
                case Status.Info: g.FillEllipse(brush, RectangleF.Inflate(r, -r.Width * 0.15f, -r.Height * 0.15f)); break;
            }
        g.SmoothingMode = old;
    }

    /// <summary>The "new" mark: the info circle in the HUD, a sparkle in soft themes (docs/06-iconography.md).</summary>
    public static void DrawNewMark(Graphics g, Rectangle r, Color color)
    {
        if (Hud) { DrawStatus(g, Status.Info, r, color); return; }
        DrawSparkle(g, RectangleF.Inflate(r, r.Width * 0.2f, r.Height * 0.2f), color);
    }

    /// <summary>A four-point sparkle (✦), for decoration and "new".</summary>
    public static void DrawSparkle(Graphics g, RectangleF r, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rx = r.Width / 2, ry = r.Height / 2, k = 0.22f;
        using var path = new GraphicsPath();
        // Concave sides: each arm is a curve pulled in towards the centre.
        path.AddBezier(cx, cy - ry, cx + rx * k, cy - ry * k, cx + rx * k, cy - ry * k, cx + rx, cy);
        path.AddBezier(cx + rx, cy, cx + rx * k, cy + ry * k, cx + rx * k, cy + ry * k, cx, cy + ry);
        path.AddBezier(cx, cy + ry, cx - rx * k, cy + ry * k, cx - rx * k, cy + ry * k, cx - rx, cy);
        path.AddBezier(cx - rx, cy, cx - rx * k, cy - ry * k, cx - rx * k, cy - ry * k, cx, cy - ry);
        path.CloseFigure();
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
        g.SmoothingMode = old;
    }

    static GraphicsPath Heart(RectangleF r)
    {
        // A unit heart (0..1), lifted to sit centred in the box.
        PointF P(float x, float y) => new(r.X + x * r.Width, r.Y + (y + 0.05f) * r.Height);
        var p = new GraphicsPath();
        p.AddBezier(P(0.5f, 0.9f), P(0.1f, 0.62f), P(0f, 0.42f), P(0f, 0.28f));
        p.AddBezier(P(0f, 0.28f), P(0f, 0.1f), P(0.14f, 0f), P(0.28f, 0f));
        p.AddBezier(P(0.28f, 0f), P(0.4f, 0f), P(0.5f, 0.1f), P(0.5f, 0.2f));
        p.AddBezier(P(0.5f, 0.2f), P(0.5f, 0.1f), P(0.6f, 0f), P(0.72f, 0f));
        p.AddBezier(P(0.72f, 0f), P(0.86f, 0f), P(1f, 0.1f), P(1f, 0.28f));
        p.AddBezier(P(1f, 0.28f), P(1f, 0.42f), P(0.9f, 0.62f), P(0.5f, 0.9f));
        p.CloseFigure();
        return p;
    }

    static void BrokenHeart(Graphics g, Brush brush, RectangleF r)
    {
        // Two halves split along a zigzag and nudged apart.
        float cx = r.X + r.Width / 2, gap = Math.Max(1.5f, r.Width * 0.14f);
        var crack = new[]
        {
            new PointF(cx, r.Y), new PointF(cx + r.Width * 0.1f, r.Y + r.Height * 0.38f),
            new PointF(cx - r.Width * 0.08f, r.Y + r.Height * 0.58f), new PointF(cx + r.Width * 0.04f, r.Bottom),
        };
        foreach (int side in new[] { -1, 1 })
        {
            using var half = new GraphicsPath();
            float edge = side < 0 ? r.X - r.Width : r.Right + r.Width;
            half.AddLines(crack);
            half.AddLine(crack[^1], new PointF(edge, r.Bottom));
            half.AddLine(new PointF(edge, r.Bottom), new PointF(edge, r.Y));
            half.CloseFigure();
            // Cut the half out of the whole heart, then slide it outwards.
            using var heart = Heart(r);
            var state = g.Save();
            g.TranslateTransform(side * gap / 2, 0);
            g.SetClip(half, CombineMode.Intersect);
            g.FillPath(brush, heart);
            g.Restore(state);
        }
    }

    static GraphicsPath Moon(RectangleF r)
    {
        // A crescent: the outer circle minus the same circle shifted right.
        float d = Math.Min(r.Width, r.Height), radius = d / 2, shift = radius * 0.75f;
        var outer = new RectangleF(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
        var inner = outer with { X = outer.X + shift };
        float a = (float)(Math.Acos(shift / 2 / radius) * 180 / Math.PI);
        var p = new GraphicsPath();
        p.AddArc(outer, a, 360 - 2 * a);
        p.AddArc(inner, 180 + a, -2 * a);
        p.CloseFigure();
        return p;
    }

    // ---- Reduced motion ------------------------------------------------------------------

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint action, uint param, out bool value, uint winIni);

    /// <summary>
    /// The native equivalent of prefers-reduced-motion: Windows Settings › Accessibility ›
    /// Visual effects › Animation effects. When off, durations are 0 and loops stop.
    /// </summary>
    public static bool ReducedMotion => !(SystemParametersInfo(0x1042 /* SPI_GETCLIENTAREAANIMATION */, 0, out var on, 0) && on);

    /// <summary>The HUD curve --ease-out-expo, cubic-bezier(0.16, 1, 0.3, 1), approximated.</summary>
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
