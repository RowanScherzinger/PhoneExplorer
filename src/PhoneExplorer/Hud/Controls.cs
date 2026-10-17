using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace PhoneExplorer.Hud;

public enum ButtonKind { Primary, Secondary, Ghost, Danger }

/// <summary>Native window chrome: dark title bar in the elevated black with a copper hairline border (Windows 11).</summary>
public static class HudWindow
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    public static void Apply(Form form)
    {
        int dark = 1, caption = ColorRef(Theme.ColorBgElevated), border = ColorRef(Theme.SolidBorder), text = ColorRef(Theme.ColorText);
        DwmSetWindowAttribute(form.Handle, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref dark, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 35 /* DWMWA_CAPTION_COLOR */, ref caption, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 34 /* DWMWA_BORDER_COLOR */, ref border, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 36 /* DWMWA_TEXT_COLOR */, ref text, sizeof(int));
    }
}

/// <summary>Buttons from docs/04-components.md: 28px tall (32px primary), uppercase display text.</summary>
public sealed class HudButton : Control, IButtonControl
{
    bool hot, down, keyboardFocus;

    public HudButton(string text, ButtonKind kind)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Kind = kind;
        Text = text.ToUpperInvariant();
        Font = Theme.Font(Theme.FontDisplayBold, Theme.TextXs);
        Cursor = Cursors.Hand;
        Margin = new Padding(Theme.Space2, 0, 0, 0);
        var w = Theme.MeasureTracked(null, Text, Font, Theme.TrackingWide).Width + Theme.Space4 * 2;
        Size = new Size(Math.Max(w, 88), kind == ButtonKind.Primary ? Theme.PrimaryHeight : Theme.ControlHeight);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonKind Kind { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DialogResult DialogResult { get; set; }

    public void NotifyDefault(bool value) { }

    public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (DialogResult != DialogResult.None && FindForm() is { } f) f.DialogResult = DialogResult;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = down = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); keyboardFocus = false; down = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); keyboardFocus = (MouseButtons == MouseButtons.None); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); keyboardFocus = false; Invalidate(); }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is Keys.Space or Keys.Enter) PerformClick();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var under = Parent?.BackColor ?? Theme.ColorBg;
        g.Clear(under);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        float a = Enabled ? 1 : Theme.DisabledOpacity;
        (Color? fill, Color? border, Color text) = Kind switch
        {
            ButtonKind.Primary => ((Color?)(hot ? Theme.ColorAccentHover : Theme.ColorAccent), (Color?)null, Theme.ColorOnAccent),
            ButtonKind.Secondary => (hot ? Theme.Alpha(Theme.ColorAccent, 0.22f) : Theme.ColorAccentSoft, hot ? Theme.ColorBorderStrong : Theme.ColorBorder, Theme.ColorAccentHover),
            ButtonKind.Danger => (Theme.ColorDangerFill, Theme.ColorDangerBorder, Theme.ColorDangerText),
            _ => (hot ? Theme.ColorAccentSoft : null, hot ? Theme.ColorBorder : Theme.ColorDivider, hot ? Theme.ColorAccentHover : Theme.ColorTextMuted),
        };
        if (down && Enabled) r.Offset(0, 1);
        if (fill is { } f) { using var b = new SolidBrush(Theme.Alpha(f, a)); g.FillRectangle(b, r); }
        if (border is { } c) { using var p = new Pen(Theme.Alpha(c, a)); g.DrawRectangle(p, r); }
        var color = Enabled ? text : Theme.Over(Theme.Alpha(text, Theme.DisabledOpacity), under);
        var size = Theme.MeasureTracked(g, Text, Font, Theme.TrackingWide);
        Theme.DrawTracked(g, Text, Font, color, new Point(r.X + (r.Width - size.Width) / 2, r.Y + (r.Height - size.Height) / 2), Theme.TrackingWide);
        if (Focused && keyboardFocus) Theme.DrawFocusRing(g, new Rectangle(2, 2, Width - 4, Height - 4), 0);
    }
}

/// <summary>.hud-label: display face, 10px, uppercase, 0.18em tracking, muted.</summary>
public sealed class HudLabel : Control
{
    public HudLabel(string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Text = text.ToUpperInvariant();
        Font = Theme.Label();
        AutoSize = true;
    }

    public override Size GetPreferredSize(Size proposed)
    {
        var s = Theme.MeasureTracked(null, Text, Font, Theme.TrackingLabel);
        return new Size(s.Width + 2, s.Height + 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.ColorBg);
        Theme.DrawTracked(e.Graphics, Text, Font, Theme.ColorTextMuted, new Point(0, 1), Theme.TrackingLabel);
    }
}

/// <summary>Check box: square box with a hairline, gold mark when checked (Proposed: the guide only specifies toggles).</summary>
public sealed class HudCheck : Control
{
    bool isChecked, hot, keyboardFocus;

    public HudCheck(string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Text = text;
        Font = Theme.Mono(Theme.TextSm);
        Cursor = Cursors.Hand;
        AutoSize = true;
        Margin = Padding.Empty;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked { get => isChecked; set { isChecked = value; Invalidate(); } }

    public override Size GetPreferredSize(Size proposed) =>
        new(LogicalToDeviceUnits(14 + Theme.Space2) + TextRenderer.MeasureText(Text, Font).Width, LogicalToDeviceUnits(Theme.ControlHeight));

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); keyboardFocus = false; }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); keyboardFocus = MouseButtons == MouseButtons.None; Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); if (e.KeyCode == Keys.Space) OnClick(EventArgs.Empty); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.ColorBg);
        int s = LogicalToDeviceUnits(14);
        var box = new Rectangle(1, (Height - s) / 2, s, s);
        if (isChecked) { using var b = new SolidBrush(Theme.ColorHighlightSoft); g.FillRectangle(b, box); }
        using (var p = new Pen(isChecked ? Theme.ColorHighlight : hot ? Theme.ColorBorderStrong : Theme.ColorBorder))
            g.DrawRectangle(p, box);
        if (isChecked)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.ColorHighlight, Theme.LineThin * DeviceDpi / 96f) { LineJoin = LineJoin.Miter, StartCap = LineCap.Square, EndCap = LineCap.Square };
            g.DrawLines(pen, new[] { new PointF(box.X + s * 0.22f, box.Y + s * 0.52f), new PointF(box.X + s * 0.42f, box.Y + s * 0.72f), new PointF(box.X + s * 0.78f, box.Y + s * 0.3f) });
        }
        var tr = new Rectangle(box.Right + LogicalToDeviceUnits(Theme.Space2), 0, Width - box.Right, Height);
        TextRenderer.DrawText(g, Text, Font, tr, isChecked || hot ? Theme.ColorText : Theme.ColorTextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        if (Focused && keyboardFocus) Theme.DrawFocusRing(g, new Rectangle(0, 0, Width, Height), -1);
    }
}

/// <summary>Input from docs/04-components.md: mono text, faint fill, copper hairline that strengthens on focus.</summary>
public sealed class HudTextBox : Panel
{
    public readonly TextBox Box = new() { BorderStyle = BorderStyle.None };

    public HudTextBox(Color background)
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Theme.Over(Theme.ColorInputFill, background);
        Box.BackColor = BackColor;
        Box.ForeColor = Theme.ColorText;
        Box.Font = Theme.Mono(Theme.TextSm);
        Height = LogicalToDeviceUnits(Theme.ControlHeight);
        Padding = new Padding(Theme.Space2, 0, Theme.Space2, 0);
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Click += (_, _) => Box.Focus();
    }

    [AllowNull] public override string Text { get => Box.Text; set => Box.Text = value ?? ""; }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Box.SetBounds(Padding.Left, (Height - Box.Height) / 2, Width - Padding.Horizontal, Box.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var p = new Pen(Box.Focused ? Theme.ColorBorderStrong : Theme.ColorBorder);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>Tooltips as small glass panels with mono text (docs/10-data-viz.md).</summary>
public static class HudToolTip
{
    public static ToolTip Create()
    {
        var tip = new ToolTip { OwnerDraw = true, InitialDelay = 600, ReshowDelay = 200, AutoPopDelay = 20000 };
        var font = Theme.Mono(Theme.TextXs);
        tip.Popup += (_, e) =>
        {
            var text = tip.GetToolTip(e.AssociatedControl);
            var size = TextRenderer.MeasureText(text, font);
            e.ToolTipSize = new Size(size.Width + Theme.Space3 * 2, size.Height + Theme.Space2 * 2);
        };
        tip.Draw += (_, e) =>
        {
            using var b = new SolidBrush(Theme.SolidSurfaceRaised);
            e.Graphics.FillRectangle(b, e.Bounds);
            using var p = new Pen(Theme.SolidBorderStrong);
            e.Graphics.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, font, new Point(Theme.Space3, Theme.Space2), Theme.ColorText);
        };
        return tip;
    }
}

/// <summary>
/// Modal from docs/04-components.md: raised glass, brackets always visible, a copper heading,
/// sentence-case message and verb-first buttons. Replaces MessageBox, whose white surface breaks dark-only.
/// </summary>
public sealed class HudDialog : Form
{
    readonly string heading;

    HudDialog(string heading, string message, (string Text, ButtonKind Kind, DialogResult Result)[] buttons)
    {
        this.heading = heading.ToUpperInvariant();
        Text = heading;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.SolidSurfaceRaised;
        Padding = new Padding(Theme.Space6);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var body = new Label
        {
            Text = message, AutoSize = true, ForeColor = Theme.ColorText, Font = Theme.Sans(Theme.TextMd),
            MaximumSize = new Size(LogicalToDeviceUnits(460), 0), Margin = new Padding(0, 0, 0, Theme.Space6),
        };
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = BackColor };
        foreach (var (text, kind, result) in buttons.Reverse())
        {
            var b = new HudButton(text, kind) { DialogResult = result };
            row.Controls.Add(b);
            // Enter confirms a primary action, never a destructive one: Danger must be clicked.
            if (kind is ButtonKind.Primary) AcceptButton = b;
            if (result is DialogResult.Cancel or DialogResult.No) CancelButton = b;
        }
        // Undocked, so place it below the painted heading by hand (form Padding only moves docked controls).
        var layout = new TableLayoutPanel
        {
            ColumnCount = 1, AutoSize = true, BackColor = BackColor, MinimumSize = new Size(LogicalToDeviceUnits(360), 0),
            Location = new Point(LogicalToDeviceUnits(Theme.Space6), LogicalToDeviceUnits(Theme.Space6 + Theme.Space8)),
        };
        layout.Controls.Add(body);
        layout.Controls.Add(row);
        Controls.Add(layout);
        // Start with focus on the safe choice, so a stray Space/Enter can't confirm a Danger button.
        if (CancelButton is HudButton safe) ActiveControl = safe;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = CancelButton is HudButton c ? c.DialogResult : DialogResult.Cancel; } };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var r = ClientRectangle;
        using (var p = new Pen(Theme.SolidBorderStrong)) g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
        Theme.DrawBrackets(g, r, Theme.ColorAccent, LogicalToDeviceUnits(Theme.BracketSize), Theme.LineThin);
        // .hud-heading: display, bold, wide tracking, copper, hairline underneath
        int x = LogicalToDeviceUnits(Theme.Space6), y = LogicalToDeviceUnits(Theme.Space5);
        var font = Theme.Heading(Theme.TextXs);
        var h = Theme.MeasureTracked(g, heading, font, Theme.TrackingWide).Height;
        Theme.DrawTracked(g, heading, font, Theme.ColorAccent, new Point(x, y), Theme.TrackingWide);
        using var line = new Pen(Theme.Over(Theme.ColorBorder, BackColor));
        int ly = y + h + LogicalToDeviceUnits(Theme.Space2);
        g.DrawLine(line, x, ly, r.Width - x, ly);
    }

    public static DialogResult Show(IWin32Window? owner, string heading, string message, params (string, ButtonKind, DialogResult)[] buttons)
    {
        using var d = new HudDialog(heading, message, buttons.Length > 0 ? buttons : new[] { ("OK", ButtonKind.Primary, DialogResult.OK) });
        return d.ShowDialog(owner);
    }
}

/// <summary>
/// Progress in the status bar: a thin scan bar while the amount of work is unknown, a segmented
/// bar (docs/10-data-viz.md) with a tabular count once it is known. Never a native progress bar.
/// </summary>
public sealed class HudProgressItem : ToolStripItem
{
    readonly System.Windows.Forms.Timer clock = new() { Interval = 33 };
    readonly DateTime started = DateTime.UtcNow;
    int value, maximum;
    bool indeterminate;

    public HudProgressItem()
    {
        AutoSize = false;
        Size = new Size(220, 22);
        clock.Tick += (_, _) => Invalidate();
    }

    public void SetIndeterminate()
    {
        indeterminate = true;
        if (!Theme.ReducedMotion) clock.Start();
        Invalidate();
    }

    public void SetProgress(int done, int total)
    {
        indeterminate = false;
        clock.Stop();
        value = done;
        maximum = total;
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) clock.Stop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int dpi = Owner?.DeviceDpi ?? 96;
        int h = Math.Max(2, 2 * dpi / 96);
        if (indeterminate)
        {
            var track = new Rectangle(0, (Height - h) / 2, Width, h);
            using (var b = new SolidBrush(Theme.Over(Theme.ColorDivider, Theme.ColorBgElevated))) g.FillRectangle(b, track);
            if (Theme.ReducedMotion) return;
            float t = (float)((DateTime.UtcNow - started).TotalMilliseconds % Theme.LoopPulse) / Theme.LoopPulse;
            int seg = Width / 4, x = (int)(-seg + (Width + seg) * t);
            var sweep = new Rectangle(x, track.Y, seg, h);
            using var grad = new LinearGradientBrush(new Rectangle(x - 1, 0, seg + 2, 1), Color.Transparent, Theme.ColorHighlight, LinearGradientMode.Horizontal);
            g.FillRectangle(grad, sweep);
            return;
        }
        const int segments = 20;
        var label = $"{value:N0}/{maximum:N0}";
        var font = Theme.Mono(Theme.TextXs);
        var lw = TextRenderer.MeasureText(label, font).Width;
        TextRenderer.DrawText(g, label, font, new Rectangle(Width - lw, 0, lw, Height), Theme.ColorHighlight, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        int barW = Width - lw - Theme.Space2, gap = 2 * dpi / 96, sh = 8 * dpi / 96;
        float sw = (barW - gap * (segments - 1)) / (float)segments;
        int lit = maximum == 0 ? 0 : (int)Math.Round(segments * (double)value / maximum);
        using var on = new SolidBrush(Theme.ColorHighlight);
        using var off = new SolidBrush(Theme.Over(Theme.ColorDivider, Theme.ColorBgElevated));
        for (int i = 0; i < segments; i++)
            g.FillRectangle(i < lit ? on : off, i * (sw + gap), (Height - sh) / 2f, sw, sh);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) clock.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>A readout in a status bar: gold tabular value, then an uppercase unit label (docs/09-voice-and-copy.md: Numbers).</summary>
public sealed class HudReadoutItem : ToolStripItem
{
    readonly List<(string Value, string Unit)> parts = new();

    public HudReadoutItem() { AutoSize = false; Size = new Size(10, 22); }

    public void Set(params (string Value, string Unit)[] values)
    {
        parts.Clear();
        parts.AddRange(values);
        using var g = Owner?.CreateGraphics();
        Width = Measure(g) + Theme.Space3 * 2;
        Invalidate();
    }

    int Measure(Graphics? g)
    {
        int w = 0;
        foreach (var (v, u) in parts)
            w += TextRenderer.MeasureText(v, Theme.Mono(Theme.TextSm), Size.Empty, TextFormatFlags.NoPadding).Width + Theme.Space1
               + Theme.MeasureTracked(g, u, Theme.Label(), Theme.TrackingLabel).Width + Theme.Space3;
        return w;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int x = Theme.Space3;
        var vf = Theme.Mono(Theme.TextSm);
        var lf = Theme.Label();
        foreach (var (v, u) in parts)
        {
            var vs = TextRenderer.MeasureText(v, vf, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, v, vf, new Point(x, (Height - vs.Height) / 2), Theme.ColorHighlight, TextFormatFlags.NoPadding);
            x += vs.Width + Theme.Space1;
            var us = Theme.MeasureTracked(g, u, lf, Theme.TrackingLabel);
            x += Theme.DrawTracked(g, u, lf, Theme.ColorTextMuted, new Point(x, (Height - us.Height) / 2 + 1), Theme.TrackingLabel) + Theme.Space3;
        }
    }
}
