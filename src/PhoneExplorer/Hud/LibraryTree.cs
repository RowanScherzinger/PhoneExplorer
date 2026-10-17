namespace PhoneExplorer.Hud;

/// <summary>What a tree row shows: a label, a count, and whether it carries the "new" info mark.</summary>
public sealed record TreeRow(object Key, string Label, int Count, bool Info = false);

/// <summary>
/// The year/month navigation list. Owner-drawn rows (docs/04-components.md: Tables and lists):
/// no zebra, hover in --color-accent-soft, selection in gold with a 2px edge, counts right-aligned
/// and tabular. Chevrons are drawn, not glyphs.
/// </summary>
public sealed class LibraryTree : TreeView
{
    public LibraryTree()
    {
        DrawMode = TreeViewDrawMode.OwnerDrawAll;
        BorderStyle = BorderStyle.None;
        FullRowSelect = true;
        HotTracking = true;
        HideSelection = false;
        ShowLines = ShowPlusMinus = ShowRootLines = false;
        BackColor = Theme.ColorBg;
        ForeColor = Theme.ColorText;
        Font = Theme.Mono(Theme.TextSm);
        ItemHeight = LogicalToDeviceUnits(Theme.ControlHeight);
        Indent = LogicalToDeviceUnits(Theme.Space4);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    int S(int v) => LogicalToDeviceUnits(v);

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (e.Bounds.IsEmpty || e.Node?.Tag is not TreeRow row) return;
        var g = e.Graphics;
        var r = new Rectangle(0, e.Bounds.Y, ClientSize.Width, e.Bounds.Height);
        bool selected = e.Node == SelectedNode, hot = (e.State & TreeNodeStates.Hot) != 0;

        using (var bg = new SolidBrush(Theme.ColorBg)) g.FillRectangle(bg, r);
        if (selected)
        {
            using var fill = new SolidBrush(Theme.Over(Theme.ColorHighlightSoft, Theme.ColorBg));
            g.FillRectangle(fill, r);
            using var edge = new SolidBrush(Theme.ColorHighlight);
            g.FillRectangle(edge, r.X, r.Y, S(2), r.Height);
        }
        else if (hot)
        {
            using var fill = new SolidBrush(Theme.Over(Theme.ColorAccentSoft, Theme.ColorBg));
            g.FillRectangle(fill, r);
        }

        int x = S(Theme.Space3) + e.Node.Level * Indent;
        int chevron = S(Theme.Space3);
        if (e.Node.Nodes.Count > 0)
        {
            int cx = x + chevron / 2, cy = r.Y + r.Height / 2, k = S(3);
            using var pen = new Pen(selected ? Theme.ColorHighlight : Theme.ColorAccent, Theme.LineThin * DeviceDpi / 96f)
                { StartCap = System.Drawing.Drawing2D.LineCap.Square, EndCap = System.Drawing.Drawing2D.LineCap.Square };
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (e.Node.IsExpanded) g.DrawLines(pen, new[] { new Point(cx - k, cy - k / 2), new Point(cx, cy + k / 2), new Point(cx + k, cy - k / 2) });
            else g.DrawLines(pen, new[] { new Point(cx - k / 2, cy - k), new Point(cx + k / 2, cy), new Point(cx - k / 2, cy + k) });
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        }
        x += chevron + S(Theme.Space1);

        if (row.Info)
        {
            int d = S(7);
            Theme.DrawShape(g, Theme.Shape.Circle, new Rectangle(x, r.Y + (r.Height - d) / 2, d, d), Theme.ColorInfo);
            x += d + S(Theme.Space2);
        }

        var countText = row.Count.ToString("N0");
        var cw = TextRenderer.MeasureText(g, countText, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        int right = r.Right - S(Theme.Space3);
        TextRenderer.DrawText(g, countText, Font, new Rectangle(right - cw, r.Y, cw, r.Height),
            selected ? Theme.ColorHighlight : Theme.ColorTextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var color = selected ? Theme.ColorTextStrong : row.Info ? Theme.ColorText : e.Node.Level == 0 ? Theme.ColorText : Theme.ColorTextMuted;
        TextRenderer.DrawText(g, row.Label, Font, new Rectangle(x, r.Y, right - cw - S(Theme.Space2) - x, r.Height), color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnNodeMouseClick(TreeNodeMouseClickEventArgs e)
    {
        base.OnNodeMouseClick(e);
        // The chevron area toggles; anywhere else just selects (native plus/minus is off).
        int x = S(Theme.Space3) + e.Node.Level * Indent;
        if (e.Node is { } node && node.Nodes.Count > 0 && e.X >= x - S(Theme.Space1) && e.X <= x + S(Theme.Space3) + S(Theme.Space1))
            node.Toggle();
    }
}
