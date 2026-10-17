using System.ComponentModel;

namespace PhoneExplorer.Hud;

public enum DeviceState { Searching, Connected, NotFound, Error }

/// <summary>
/// The sidebar's lead panel (docs/00-principles.md: lead each panel with one readout): device
/// status as shape + label, then the item count as the screen's readout. Boots in on first load.
/// </summary>
public sealed class DevicePanel : Control
{
    readonly System.Windows.Forms.Timer clock = new() { Interval = 33 };
    DateTime bootStart, pulseStart = DateTime.UtcNow;
    bool booting, hot;
    DeviceState state = DeviceState.Searching;
    string deviceName = "", detail = "", count = "";
    bool switchable;

    /// <summary>More than one phone is connected: the panel shows a chevron and a click picks the phone.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Switchable
    {
        get => switchable;
        set { switchable = value; Cursor = value ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    public DevicePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.ColorBg;
        clock.Tick += (_, _) => Invalidate();
    }

    public void Set(DeviceState state, string deviceName, string count, string detail)
    {
        this.state = state;
        this.deviceName = deviceName;
        this.count = count;
        this.detail = detail;
        // Pending pulses (docs/01-color.md: Status); everything else is steady.
        if (state == DeviceState.Searching && !Theme.ReducedMotion) clock.Start();
        else if (!booting) clock.Stop();
        Invalidate();
    }

    /// <summary>Plays the .hud-boot flicker once, on first load only (docs/05-motion.md).</summary>
    public void Boot()
    {
        if (Theme.ReducedMotion) return;
        booting = true;
        bootStart = DateTime.UtcNow;
        clock.Start();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

    int S(int v) => LogicalToDeviceUnits(v);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.ColorBg);

        float opacity = 1;
        if (booting)
        {
            float t = (float)(DateTime.UtcNow - bootStart).TotalMilliseconds / Theme.DurationBoot;
            if (t >= 1) { booting = false; if (state != DeviceState.Searching) clock.Stop(); }
            else opacity = Theme.BootOpacity(t);
        }
        Color C(Color c) => Theme.Over(Theme.Alpha(c, opacity), Theme.ColorBg);

        // Glass panel with a copper hairline; brackets rest at 45% and lock on hover.
        var panel = new Rectangle(S(Theme.Space2), S(Theme.Space2), Width - S(Theme.Space2) * 2, Height - S(Theme.Space2) * 2);
        using (var fill = new SolidBrush(C(hot ? Theme.ColorSurfaceHover : Theme.ColorSurface))) g.FillRectangle(fill, panel);
        using (var border = new Pen(C(hot ? Theme.ColorBorderStrong : Theme.ColorBorder)))
            g.DrawRectangle(border, panel.X, panel.Y, panel.Width - 1, panel.Height - 1);
        Theme.DrawBrackets(g, panel, C(Theme.Alpha(Theme.ColorAccent, hot ? 1 : Theme.BracketRestOpacity)), S(Theme.BracketSize), Theme.LineThin);

        int x = panel.X + S(Theme.Space3), y = panel.Y + S(Theme.Space3);
        var label = Theme.Label();
        Theme.DrawTracked(g, "DEVICE", label, C(Theme.ColorTextMuted), new Point(x, y), Theme.TrackingLabel);
        y += S(Theme.Space4);

        var (shape, color, word) = state switch
        {
            DeviceState.Connected => (Theme.Shape.DiamondFilled, Theme.ColorStatusOk, "CONNECTED"),
            DeviceState.Searching => (Theme.Shape.DiamondHollow, Theme.ColorStatusPending, "SEARCHING"),
            DeviceState.Error => (Theme.Shape.SquareFilled, Theme.ColorStatusDanger, "ERROR"),
            _ => (Theme.Shape.SquareHollow, Theme.ColorStatusIdle, "NO DEVICE"),
        };
        if (state == DeviceState.Searching && !Theme.ReducedMotion)
        {
            // pulse: 1 -> 0.45 -> 1 over --loop-pulse
            double p = (DateTime.UtcNow - pulseStart).TotalMilliseconds % Theme.LoopPulse / Theme.LoopPulse;
            color = Theme.Over(Theme.Alpha(color, (float)(0.725 + 0.275 * Math.Cos(p * 2 * Math.PI))), Theme.SolidSurface);
        }
        int sh = S(9);
        Theme.DrawShape(g, shape, new Rectangle(x, y + S(2), sh, sh), C(color));
        int wx = x + sh + S(Theme.Space2);
        wx += Theme.DrawTracked(g, word, Theme.Heading(Theme.Text2xs), C(color), new Point(wx, y + 1), Theme.TrackingLabel);
        int chevron = switchable ? S(Theme.Space4) : 0;
        if (deviceName.Length > 0)
            TextRenderer.DrawText(g, deviceName, Theme.Mono(Theme.TextSm), new Rectangle(wx + S(Theme.Space2), y - 1, panel.Right - wx - S(Theme.Space4) - chevron, S(18)),
                C(Theme.ColorText), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if (switchable)
        {
            // Down chevron at the end of the name row: click to pick another phone.
            int cx = panel.Right - S(Theme.Space3) - chevron / 2, cy = y + S(6), k = S(3);
            using var pen = new Pen(C(hot ? Theme.ColorHighlight : Theme.ColorAccent), Theme.LineThin * DeviceDpi / 96f)
                { StartCap = System.Drawing.Drawing2D.LineCap.Square, EndCap = System.Drawing.Drawing2D.LineCap.Square };
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawLines(pen, new[] { new Point(cx - k, cy - k / 2), new Point(cx, cy + k / 2), new Point(cx + k, cy - k / 2) });
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        }
        y += S(Theme.Space6);

        // The readout: big light mono numerals in gold, unit after.
        var valueFont = Theme.Font(Theme.FontMonoLight, Theme.Text3xl);
        var vs = TextRenderer.MeasureText(g, count, valueFont, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, count, valueFont, new Point(x - S(1), y), C(Theme.ColorHighlight), TextFormatFlags.NoPadding);
        var unitFont = Theme.Label(Theme.TextXs);
        var us = Theme.MeasureTracked(g, "ITEMS", unitFont, Theme.TrackingLabel);
        if (count.Length > 0)
            Theme.DrawTracked(g, "ITEMS", unitFont, C(Theme.ColorTextMuted), new Point(x + vs.Width + S(Theme.Space1), y + vs.Height - us.Height - S(4)), Theme.TrackingLabel);
        y += vs.Height + S(Theme.Space2);

        TextRenderer.DrawText(g, detail, Theme.Mono(Theme.TextXs), new Rectangle(x, y, panel.Right - x - S(Theme.Space3), S(16)),
            C(Theme.ColorTextMuted), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) clock.Dispose();
        base.Dispose(disposing);
    }
}
