using System.Drawing.Drawing2D;

namespace PhoneExplorer.Hud;

/// <summary>How a ToolStrip item is drawn. Set with HudRenderer.Style().</summary>
public enum ItemRole { Ghost, Toggle, Segment, Primary, Secondary, Danger, Label, Text }

/// <summary>Renders toolbars, the status bar and context menus in the current theme (docs/04-components.md).</summary>
public sealed class HudRenderer : ToolStripProfessionalRenderer
{
    public HudRenderer() : base(new HudColors()) { RoundedEdges = false; }

    static readonly Dictionary<ToolStripItem, ItemRole> roles = new();
    /// <summary>Popup windows already given the current theme's corners.</summary>
    static readonly Dictionary<IntPtr, ThemeDef> popups = new();

    /// <summary>Applies a role: font, and padding for the letter-spacing and case the renderer adds.</summary>
    public static T Style<T>(T item, ItemRole role) where T : ToolStripItem
    {
        roles[item] = role;
        item.Font = role switch
        {
            ItemRole.Label => Theme.Label(),
            ItemRole.Text => Theme.Body(Theme.TextSm),
            _ => Theme.ButtonFont(),
        };
        if (role is not ItemRole.Text)
        {
            // Native layout measures the plain text; add the drawn (cased, tracked) width as padding.
            var tracked = Theme.MeasureTracked(null, Theme.Case(item.Text ?? ""), item.Font, Tracking(role)).Width;
            var plain = TextRenderer.MeasureText(item.Text ?? "", item.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            int pad = role is ItemRole.Label ? 2 : Theme.Current.Pills ? Theme.Space4 : Theme.Space3;
            int extra = Math.Max(0, tracked - plain);
            item.Padding = new Padding(pad + extra / 2, 0, pad + extra - extra / 2, 0);
        }
        if (item is ToolStripButton b && role is ItemRole.Primary or ItemRole.Secondary or ItemRole.Ghost or ItemRole.Toggle or ItemRole.Segment)
        {
            b.AutoSize = true;
            b.Margin = new Padding(role == ItemRole.Segment ? 0 : 2, 3, role == ItemRole.Segment ? 0 : 2, 3);
        }
        return item;
    }

    /// <summary>Re-applies every styled item's role after a theme change (fonts and padding depend on it).</summary>
    public static void Restyle(ToolStrip strip)
    {
        foreach (ToolStripItem item in strip.Items)
        {
            if (roles.TryGetValue(item, out var role)) Style(item, role);
            if (item is ToolStripDropDownItem { HasDropDownItems: true } d) Restyle(d.DropDown);
        }
        strip.Invalidate();
    }

    static float Tracking(ItemRole role) => role == ItemRole.Label ? Theme.TrackingLabel : Theme.TrackingWide;

    static ItemRole RoleOf(ToolStripItem item) =>
        roles.TryGetValue(item, out var r) ? r : item.Owner is ToolStripDropDown ? ItemRole.Ghost : ItemRole.Text;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is ToolStripDropDown && e.ToolStrip.IsHandleCreated &&
            (!popups.TryGetValue(e.ToolStrip.Handle, out var applied) || applied != Theme.Current))
        {
            HudWindow.Popup(e.ToolStrip.Handle);
            popups[e.ToolStrip.Handle] = Theme.Current;
        }
        var bg = e.ToolStrip is ToolStripDropDown ? Theme.SolidSurfaceRaised : Theme.ColorBgElevated;
        using var b = new SolidBrush(bg);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var r = e.ToolStrip.ClientRectangle;
        if (e.ToolStrip is ToolStripDropDown)
        {
            // Round themes: Windows draws the rounded border (HudWindow.Popup).
            if (HudWindow.RoundPopups) return;
            using var p = new Pen(Theme.SolidBorderStrong);
            e.Graphics.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
            Theme.DrawBrackets(e.Graphics, r, Theme.ColorAccent, Theme.BracketSize * e.ToolStrip.DeviceDpi / 96, Theme.LineThin);
            return;
        }
        using var pen = new Pen(Theme.SolidBorder);
        int y = e.ToolStrip is StatusStrip ? 0 : r.Bottom - 1;
        e.Graphics.DrawLine(pen, 0, y, r.Right, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Over(Theme.ColorDivider, Theme.ColorBgElevated));
        var r = e.Item.ContentRectangle;
        if (e.Vertical) { int x = r.X + r.Width / 2; e.Graphics.DrawLine(pen, x, r.Y + 4, x, r.Bottom - 4); }
        else { int y = r.Y + r.Height / 2; e.Graphics.DrawLine(pen, r.X + 4, y, r.Right - 4, y); }
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) => PaintBackground(e.Graphics, e.Item);
    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) => PaintBackground(e.Graphics, e.Item);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var r = new Rectangle(Point.Empty, e.Item.Size);
        bool danger = RoleOf(e.Item) == ItemRole.Danger;
        if (!Theme.Hud)
        {
            // Soft: a rounded row highlight, no edge bar.
            r.Inflate(-Theme.Space1, 0);
            Theme.Fill(e.Graphics, danger ? Theme.ColorDangerFill : Theme.ColorAccentSoft, r, Theme.Corner.Sm);
            return;
        }
        r.Inflate(-2, 0);
        using var fill = new SolidBrush(danger ? Theme.ColorDangerFill : Theme.ColorAccentSoft);
        e.Graphics.FillRectangle(fill, r);
        using var bar = new SolidBrush(danger ? Theme.ColorStatusDanger : Theme.ColorAccent);
        e.Graphics.FillRectangle(bar, r.X, r.Y, 2, r.Height);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // A drawn tick in the selection colour (the default is a blue box).
        var r = e.ImageRectangle;
        int s = Math.Min(r.Width, r.Height) * 2 / 3;
        float x = r.X + (r.Width - s) / 2f, y = r.Y + (r.Height - s) / 2f;
        var old = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool round = Theme.Current.RadiusSm > 0;
        using var pen = new Pen(Theme.ColorSelect, Theme.LineThin * (e.Item.Owner?.DeviceDpi ?? 96) / 96f)
        {
            LineJoin = round ? LineJoin.Round : LineJoin.Miter,
            StartCap = round ? LineCap.Round : LineCap.Square,
            EndCap = round ? LineCap.Round : LineCap.Square,
        };
        e.Graphics.DrawLines(pen, new[] { new PointF(x + s * 0.1f, y + s * 0.55f), new PointF(x + s * 0.4f, y + s * 0.85f), new PointF(x + s * 0.95f, y + s * 0.2f) });
        e.Graphics.SmoothingMode = old;
    }

    static void PaintBackground(Graphics g, ToolStripItem item)
    {
        var role = RoleOf(item);
        var r = new Rectangle(Point.Empty, item.Size);
        bool on = item is ToolStripButton { Checked: true };
        bool hot = item.Selected && item.Enabled, down = item.Pressed;
        float a = item.Enabled ? 1 : Theme.DisabledOpacity;

        Color? fill = null, border = null;
        switch (role)
        {
            case ItemRole.Primary:
                fill = down ? Theme.ColorAccent : hot ? Theme.ColorAccentHover : Theme.ColorAccent;
                break;
            case ItemRole.Secondary:
                fill = hot ? Theme.Alpha(Theme.ColorAccent, 0.22f) : Theme.ColorAccentSoft;
                border = hot ? Theme.ColorBorderStrong : Theme.ColorBorder;
                break;
            case ItemRole.Toggle or ItemRole.Segment:
                if (on) { fill = Theme.ColorAccentSoft; border = Theme.ColorBorderStrong; }
                else if (hot) border = Theme.ColorBorder;
                else if (role == ItemRole.Segment) border = Theme.ColorDivider;
                break;
            case ItemRole.Ghost:
                border = hot ? Theme.ColorBorder : Theme.ColorDivider;
                if (hot) fill = Theme.ColorAccentSoft;
                break;
            case ItemRole.Danger:
                fill = Theme.ColorDangerFill;
                border = hot ? Theme.ColorStatusDanger : Theme.ColorDangerBorder;
                break;
        }
        if (fill is { } f) Theme.Fill(g, Theme.Alpha(f, a), r, Theme.Corner.Pill);
        if (border is { } c) Theme.Stroke(g, Theme.Alpha(c, a), r, Theme.Corner.Pill);
        if (role is ItemRole.Primary && hot && item.Enabled && Theme.Hud)
        {
            // --glow-sm on the primary action
            using var glow = new Pen(Theme.Alpha(Theme.ColorAccent, 0.35f), 2);
            g.DrawRectangle(glow, -1, -1, r.Width + 1, r.Height + 1);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var item = e.Item;
        var role = RoleOf(item);
        bool on = item is ToolStripButton { Checked: true };
        bool hot = item.Selected && item.Enabled;
        Color color = role switch
        {
            ItemRole.Primary => Theme.ColorOnAccent,
            ItemRole.Secondary => Theme.ColorAccentHover,
            ItemRole.Toggle or ItemRole.Segment => on ? Theme.ColorAccentHover : hot ? Theme.ColorAccent : Theme.ColorTextMuted,
            ItemRole.Ghost => hot ? Theme.ColorAccentHover : Theme.ColorTextMuted,
            ItemRole.Danger => Theme.ColorDangerText,
            ItemRole.Label => Theme.ColorEyebrow,
            _ => Theme.ColorTextMuted,
        };
        if (!item.Enabled)
            color = Theme.Over(Theme.Alpha(color, Theme.DisabledOpacity), item.Owner is ToolStripDropDown ? Theme.SolidSurfaceRaised : Theme.ColorBgElevated);
        if (role is ItemRole.Text)
        {
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, color, e.TextFormat | TextFormatFlags.EndEllipsis);
            return;
        }
        // Menu items get this call twice: once for the name, once for the shortcut ("Ctrl+C").
        // The shortcut sits right-aligned in its own column, in small dim mono, untracked.
        if (item is ToolStripMenuItem && e.Text != item.Text)
        {
            var hint = item.Enabled ? Theme.ColorTextDim : Theme.Over(Theme.Alpha(Theme.ColorTextDim, Theme.DisabledOpacity), Theme.SolidSurfaceRaised);
            TextRenderer.DrawText(e.Graphics, e.Text, Theme.Mono(Theme.TextXs), e.TextRectangle, hint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }
        var font = e.TextFont ?? item.Font;
        var text = Theme.Case(e.Text ?? "");
        var size = Theme.MeasureTracked(e.Graphics, text, font, Tracking(role));
        var bounds = item.ContentRectangle;
        var origin = new Point(
            item.Owner is ToolStripDropDown ? e.TextRectangle.X : bounds.X + (bounds.Width - size.Width) / 2,
            bounds.Y + (bounds.Height - size.Height) / 2);
        Theme.DrawTracked(e.Graphics, text, font, color, origin, Tracking(role));
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.ColorTextMuted;
        base.OnRenderArrow(e);
    }

    sealed class HudColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.SolidSurfaceRaised;
        public override Color ImageMarginGradientBegin => Theme.SolidSurfaceRaised;
        public override Color ImageMarginGradientMiddle => Theme.SolidSurfaceRaised;
        public override Color ImageMarginGradientEnd => Theme.SolidSurfaceRaised;
        public override Color MenuBorder => Theme.SolidBorderStrong;
        public override Color MenuItemBorder => Theme.SolidBorder;
        public override Color SeparatorDark => Theme.Over(Theme.ColorDivider, Theme.ColorBgElevated);
        public override Color SeparatorLight => Theme.Over(Theme.ColorDivider, Theme.ColorBgElevated);
        public override Color StatusStripGradientBegin => Theme.ColorBgElevated;
        public override Color StatusStripGradientEnd => Theme.ColorBgElevated;
        public override Color ToolStripGradientBegin => Theme.ColorBgElevated;
        public override Color ToolStripGradientMiddle => Theme.ColorBgElevated;
        public override Color ToolStripGradientEnd => Theme.ColorBgElevated;
    }
}
