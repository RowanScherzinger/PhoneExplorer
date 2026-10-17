using System.ComponentModel;
using System.Drawing.Drawing2D;
using PhoneExplorer.Hud;

namespace PhoneExplorer;

public sealed record EntryGroup(string Title, int Start, int Count);

/// <summary>
/// A scrolling grid of thumbnails split into titled groups (months). Thumbnails come from
/// ThumbnailSource; the grid reports which visible ones are missing via ThumbnailsNeeded.
/// Drawn in the current theme: HUD frames with brackets that lock on hover, or soft rounded tiles.
/// </summary>
public sealed class ThumbGrid : ScrollableControl
{
    // Count == -1 marks a group header row.
    record struct Row(int Y, int Height, int Group, int First, int Count);

    readonly List<Row> rows = new();
    readonly ToolTip tip = HudToolTip.Create();
    readonly HashSet<int> selected = new();
    readonly System.Windows.Forms.Timer clock = new() { Interval = 16 };
    List<MediaEntry> entries = new();
    List<EntryGroup> groups = new();
    Rectangle[] tiles = Array.Empty<Rectangle>();
    int cols = 1, focus = -1, anchor = -1, hover = -1, hoverHeader = -1;
    int thumbSize = 150;
    string emptyText = "";
    bool keyboardFocus;
    DateTime hoverStart;

    public Func<int, Bitmap?>? ThumbnailSource;
    public Func<MediaEntry, bool>? IsNew;
    public event Action<List<int>>? ThumbnailsNeeded;
    public event EventHandler? SelectionChanged;
    public event Action<MediaEntry>? EntryActivated;
    public event Action? DeleteRequested;

    public ThumbGrid()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        AutoScroll = true;
        TabStop = true;
        ApplyTheme();
        clock.Tick += (_, _) =>
        {
            if ((DateTime.UtcNow - hoverStart).TotalMilliseconds > Theme.DurationNormal) clock.Stop();
            if (hover >= 0) InvalidateTile(hover);
        };
    }

    /// <summary>Re-reads the theme: background, label font and layout.</summary>
    public void ApplyTheme()
    {
        BackColor = Theme.ColorBg;
        Font = Theme.Body(Theme.TextSm);
        DoLayout();
        Invalidate();
    }

    /// <summary>First line is the heading; the rest is sentence-case prose.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get => emptyText; set { emptyText = value; Invalidate(); } }

    /// <summary>Thumbnail edge length in logical (96-dpi) pixels.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ThumbSize { get => thumbSize; set { thumbSize = value; DoLayout(); Invalidate(); } }

    public int ThumbPixels => S(thumbSize);

    public IReadOnlyList<MediaEntry> Entries => entries;

    public List<MediaEntry> SelectedEntries => entries.Where(e => selected.Contains(e.Key)).ToList();

    public void SetEntries(List<MediaEntry> list, List<EntryGroup> groupList, bool keepScroll)
    {
        int scroll = -AutoScrollPosition.Y;
        entries = list;
        groups = groupList;
        var keys = entries.Select(e => e.Key).ToHashSet();
        bool lost = selected.RemoveWhere(k => !keys.Contains(k)) > 0;
        focus = anchor = hover = -1;
        DoLayout();
        AutoScrollPosition = new Point(0, keepScroll ? scroll : 0);
        Invalidate();
        if (lost) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll()
    {
        foreach (var e in entries) selected.Add(e.Key);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        if (selected.Count == 0) return;
        selected.Clear();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    int S(int v) => v * DeviceDpi / 96;
    int Pad => S(Theme.Space2);
    int Gap => S(Theme.Space1);
    int Side => S(Theme.Space4);
    int LabelH => S(Theme.Space5);
    int HeaderH => S(Theme.Space12);
    int TileW => S(thumbSize) + Pad * 2;
    int TileH => S(thumbSize) + Pad * 2 + LabelH;

    void DoLayout()
    {
        rows.Clear();
        tiles = new Rectangle[entries.Count];
        int width = ClientSize.Width;
        cols = Math.Max(1, (width - Side * 2 + Gap) / (TileW + Gap));
        int y = S(Theme.Space1);
        for (int g = 0; g < groups.Count; g++)
        {
            rows.Add(new Row(y, HeaderH, g, groups[g].Start, -1));
            y += HeaderH + S(Theme.Space2);
            for (int i = 0; i < groups[g].Count; i += cols)
            {
                int n = Math.Min(cols, groups[g].Count - i);
                rows.Add(new Row(y, TileH, g, groups[g].Start + i, n));
                for (int c = 0; c < n; c++)
                    tiles[groups[g].Start + i + c] = new Rectangle(Side + c * (TileW + Gap), y, TileW, TileH);
                y += TileH + Gap;
            }
            y += S(Theme.Space3);
        }
        AutoScrollMinSize = new Size(0, y + Side);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int oldCols = cols;
        DoLayout();
        if (cols != oldCols && focus >= 0) EnsureVisible(focus);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        DoLayout();
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Invalidate();
    }

    int ScrollY => -AutoScrollPosition.Y;

    void InvalidateTile(int i)
    {
        if (i < 0 || i >= tiles.Length) return;
        var r = tiles[i];
        r.Offset(0, -ScrollY);
        r.Inflate(S(Theme.Space2), S(Theme.Space2));
        Invalidate(r);
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.ColorBg);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        if (entries.Count == 0)
        {
            DrawEmpty(g);
            return;
        }

        int top = ScrollY + e.ClipRectangle.Top, bottom = ScrollY + e.ClipRectangle.Bottom;
        foreach (var row in rows)
        {
            if (row.Y + row.Height + S(Theme.Space2) < top) continue;
            if (row.Y - S(Theme.Space2) > bottom) break;
            if (row.Count < 0) DrawHeader(g, row);
            else
                for (int i = row.First; i < row.First + row.Count; i++)
                    DrawTile(g, i);
        }
        RequestMissingThumbnails();
    }

    void DrawEmpty(Graphics g)
    {
        var lines = emptyText.Split('\n', 2);
        var heading = Theme.Case(lines[0].TrimEnd('.'));
        var body = lines.Length > 1 ? lines[1].Trim('\n') : "";
        var hf = Theme.Heading(Theme.Hud ? Theme.TextLg : Theme.TextXl);
        var hs = Theme.MeasureTracked(g, heading, hf, Theme.TrackingWide);
        var bf = Theme.Sans(Theme.TextMd);
        int width = Math.Min(ClientSize.Width - S(Theme.Space8) * 2, S(520));
        var bs = TextRenderer.MeasureText(g, body, bf, new Size(width, 0), TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
        int total = hs.Height + S(Theme.Space4) + bs.Height;
        int y = (ClientSize.Height - total) / 2;

        // HUD: a bracketed frame around the message, like a targeting reticle on empty space.
        // Soft: a small sparkle motif above it (docs/04-components.md: Empty states).
        var frame = new Rectangle((ClientSize.Width - width) / 2 - S(Theme.Space6), y - S(Theme.Space6), width + S(Theme.Space6) * 2, total + S(Theme.Space6) * 2);
        Theme.DrawBrackets(g, frame, Theme.Alpha(Theme.ColorAccent, Theme.BracketRestOpacity), S(Theme.BracketSize), Theme.LineThin);
        if (!Theme.Hud)
        {
            int m = S(Theme.Space6), my = y - m - S(Theme.Space4);
            Theme.DrawSparkle(g, new Rectangle((ClientSize.Width - m) / 2, my, m, m), Theme.ColorAccent);
            Theme.DrawSparkle(g, new Rectangle(ClientSize.Width / 2 + m / 2 + S(Theme.Space1), my - S(Theme.Space1), m / 2, m / 2), Theme.Alpha(Theme.ColorAccent, 0.6f));
        }

        Theme.DrawTracked(g, heading, hf, Theme.ColorAccentText, new Point((ClientSize.Width - hs.Width) / 2, y), Theme.TrackingWide);
        TextRenderer.DrawText(g, body, bf, new Rectangle((ClientSize.Width - width) / 2, y + hs.Height + S(Theme.Space4), width, bs.Height),
            Theme.ColorTextMuted, TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
    }

    void DrawHeader(Graphics g, Row row)
    {
        var group = groups[row.Group];
        var r = new Rectangle(Side, row.Y - ScrollY, ClientSize.Width - Side * 2, row.Height);
        int baseline = r.Bottom - S(Theme.Space2);

        // Month title: display face in the theme's case, accent (a section heading).
        var title = Theme.Case(group.Title);
        var tf = Theme.Heading(Theme.TextLg);
        var ts = Theme.MeasureTracked(g, title, tf, Theme.TrackingWide);
        int x = r.X + Theme.DrawTracked(g, title, tf, Theme.ColorAccentText, new Point(r.X, baseline - ts.Height), Theme.TrackingWide) + S(Theme.Space4);

        // Count readout: tabular value + muted unit.
        int selectedInGroup = 0;
        for (int i = group.Start; i < group.Start + group.Count; i++)
            if (selected.Contains(entries[i].Key)) selectedInGroup++;
        x = Readout(g, x, baseline, group.Count.ToString("N0"), group.Count == 1 ? "item" : "items");
        if (selectedInGroup > 0) x = Readout(g, x + S(Theme.Space2), baseline, selectedInGroup.ToString("N0"), "selected");

        if (row.Group == hoverHeader)
        {
            var hint = Theme.Case(selectedInGroup == group.Count ? "Click to clear month" : "Click to select month");
            var lf = Theme.Label();
            var hs = Theme.MeasureTracked(g, hint, lf, Theme.TrackingLabel);
            Theme.DrawTracked(g, hint, lf, Theme.ColorAccentText, new Point(r.Right - hs.Width, baseline - hs.Height - S(2)), Theme.TrackingLabel);
        }

        // Hairline that fades into the background on the right.
        int ly = r.Bottom - 1;
        using var brush = new LinearGradientBrush(new Rectangle(r.X, ly, r.Width, 1), Theme.ColorBorderStrong, Theme.Alpha(Theme.ColorBorder, 0), 0f);
        g.FillRectangle(brush, r.X, ly, r.Width, 1);
    }

    int Readout(Graphics g, int x, int baseline, string value, string unit)
    {
        var vf = Theme.Number(Theme.TextMd);
        var vs = TextRenderer.MeasureText(g, value, vf, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, value, vf, new Point(x, baseline - vs.Height - S(1)), Theme.ColorHighlight, TextFormatFlags.NoPadding);
        x += vs.Width + S(Theme.Space1);
        var lf = Theme.Label();
        unit = Theme.Case(unit);
        var ls = Theme.MeasureTracked(g, unit, lf, Theme.TrackingLabel);
        return x + Theme.DrawTracked(g, unit, lf, Theme.ColorTextMuted, new Point(x, baseline - ls.Height - S(2)), Theme.TrackingLabel);
    }

    void DrawTile(Graphics g, int i)
    {
        var entry = entries[i];
        var r = tiles[i];
        r.Offset(0, -ScrollY);
        bool isSelected = selected.Contains(entry.Key), isHover = i == hover;

        // Tile states: hover = accent-soft; selected = selection tint and ring (the selection colour is the signal).
        if (isSelected || isHover)
        {
            Theme.Fill(g, isSelected ? Theme.ColorSelectSoft : Theme.ColorAccentSoft, r, Theme.Corner.Md);
            Theme.Stroke(g, isSelected ? Theme.ColorSelectRing : Theme.ColorBorder, r, Theme.Corner.Md, isSelected && !Theme.Hud ? Theme.LineThin : Theme.LineHair);
        }

        var box = new Rectangle(r.X + Pad, r.Y + Pad, S(thumbSize), S(thumbSize));
        var bmp = ThumbnailSource?.Invoke(entry.Display.Id);
        Rectangle pic;
        if (bmp != null)
        {
            float scale = Math.Min((float)box.Width / bmp.Width, (float)box.Height / bmp.Height);
            int w = (int)(bmp.Width * scale), h = (int)(bmp.Height * scale);
            pic = new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
            int radius = Theme.Radius(g, Theme.Corner.Sm, pic);
            if (radius == 0) g.DrawImage(bmp, pic);
            else
            {
                // Rounded photo: paint the path with the picture as its texture, so the corners are smooth.
                using var texture = new TextureBrush(bmp, WrapMode.Clamp);
                texture.TranslateTransform(pic.X, pic.Y);
                texture.ScaleTransform((float)pic.Width / bmp.Width, (float)pic.Height / bmp.Height);
                using var path = Theme.RoundRect(pic, radius);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(texture, path);
                g.SmoothingMode = SmoothingMode.None;
            }
        }
        else
        {
            // Skeleton: a hairline outline of what will appear, not a grey block.
            pic = box;
            Theme.Stroke(g, Theme.ColorBorder, box, Theme.Corner.Sm);
            var lf = Theme.Label();
            var ext = entry.Display.Ext;
            var es = Theme.MeasureTracked(g, ext, lf, Theme.TrackingLabel);
            Theme.DrawTracked(g, ext, lf, Theme.ColorTextDim, new Point(box.X + (box.Width - es.Width) / 2, box.Y + (box.Height - es.Height) / 2), Theme.TrackingLabel);
        }

        // Badges on the picture: neutral data in silver, "new" is the info colour with the new mark.
        int inset = S(Theme.Space1) + (Theme.Current.RadiusSm > 0 ? S(2) : 0);
        int bx = pic.X + inset;
        if (IsNew?.Invoke(entry) == true) bx = Badge(g, "New", bx, pic.Y + inset, Theme.ColorInfo, BadgeMark.New) + S(2);
        if (entry.HasLive) Badge(g, "Live", bx, pic.Y + inset, Theme.ColorNeutralData);
        if (entry.IsEdited)
            Badge(g, "Edited", pic.Right - inset - BadgeWidth(g, "Edited", BadgeMark.None), pic.Y + inset, Theme.ColorNeutralData);
        if (entry.Kind == MediaKind.Video)
        {
            var d = entry.Primary.Duration;
            var text = d == null ? "Video" : d.Value.TotalHours >= 1 ? d.Value.ToString(@"h\:mm\:ss") : d.Value.ToString(@"m\:ss");
            Badge(g, text, pic.X + inset, pic.Bottom - inset - S(16), Theme.ColorText, BadgeMark.Play);
        }

        // HUD brackets: accent, locking on (scale in from 115%) on hover; steady in the selection colour when selected.
        if ((isSelected || isHover) && Theme.Hud)
        {
            var target = Rectangle.Inflate(r, -1, -1);
            float t = isHover && !isSelected && !Theme.ReducedMotion
                ? Theme.EaseOutExpo(Math.Min(1f, (float)(DateTime.UtcNow - hoverStart).TotalMilliseconds / Theme.DurationNormal))
                : 1f;
            float grow = (1 - t) * 0.15f;
            var br = Rectangle.Inflate(target, (int)(target.Width * grow / 2), (int)(target.Height * grow / 2));
            Theme.DrawBrackets(g, br, Theme.Alpha(isSelected ? Theme.ColorSelect : Theme.ColorAccent, t), S(Theme.BracketSize), Theme.LineThin);
        }

        if (isSelected)
        {
            // Selection mark: the selection colour (a square, or a dot in round themes) with an on-accent tick.
            int d = Theme.Hud ? S(Theme.Space4) : S(Theme.Space5);
            var c = new Rectangle(pic.Right - d - inset, pic.Bottom - d - inset, d, d);
            Theme.Fill(g, Theme.ColorSelect, c, Theme.Corner.Pill);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var cap = Theme.Hud ? LineCap.Flat : LineCap.Round;
            using var pen = new Pen(Theme.ColorOnAccent, S(2)) { LineJoin = Theme.Hud ? LineJoin.Miter : LineJoin.Round, StartCap = cap, EndCap = cap };
            g.DrawLines(pen, new[] { new PointF(c.X + d * 0.22f, c.Y + d * 0.52f), new PointF(c.X + d * 0.42f, c.Y + d * 0.72f), new PointF(c.X + d * 0.78f, c.Y + d * 0.3f) });
            g.SmoothingMode = SmoothingMode.None;
        }

        var label = new Rectangle(r.X + Pad, box.Bottom + S(Theme.Space1), box.Width, LabelH - S(Theme.Space1));
        TextRenderer.DrawText(g, entry.Primary.Name, Font, label, isSelected ? Theme.ColorTextStrong : Theme.ColorTextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        if (i == focus && Focused && keyboardFocus) Theme.DrawFocusRing(g, r, S(2), Theme.Corner.Md);
    }

    enum BadgeMark { None, New, Play }

    int BadgeWidth(Graphics g, string text, BadgeMark mark) =>
        Theme.MeasureTracked(g, Theme.Case(text), Theme.Label(), Theme.TrackingLabel).Width
        + (mark == BadgeMark.None ? 0 : S(6) + S(Theme.Space1)) + S(Theme.Space2) * 2 - S(2);

    /// <summary>A small chip over the photo (HUD: square, --radius-sm is sharp at this size; soft: a pill). Returns its right edge.</summary>
    int Badge(Graphics g, string text, int x, int y, Color color, BadgeMark mark = BadgeMark.None)
    {
        var r = new Rectangle(x, y, BadgeWidth(g, text, mark), S(16));
        text = Theme.Case(text);
        var lf = Theme.Label();
        var ts = Theme.MeasureTracked(g, text, lf, Theme.TrackingLabel);
        Theme.Fill(g, Theme.ColorScrim, r, Theme.Corner.Pill);
        int tx = r.X + S(Theme.Space2) - S(1);
        if (mark != BadgeMark.None)
        {
            int d = S(6);
            var sr = new Rectangle(tx, r.Y + (r.Height - d) / 2, d, d);
            if (mark == BadgeMark.Play)
            {
                // Play mark: a right-pointing triangle polygon.
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var b = new SolidBrush(color);
                g.FillPolygon(b, new[] { new Point(sr.X, sr.Y), new Point(sr.Right, sr.Y + d / 2), new Point(sr.X, sr.Bottom) });
                g.SmoothingMode = SmoothingMode.None;
            }
            else Theme.DrawNewMark(g, sr, color);
            tx += d + S(Theme.Space1);
        }
        Theme.DrawTracked(g, text, lf, color, new Point(tx, r.Y + (r.Height - ts.Height) / 2), Theme.TrackingLabel);
        return r.Right;
    }

    /// <summary>Asks for thumbnails of tiles on screen first, then one screen below for smooth scrolling.</summary>
    void RequestMissingThumbnails()
    {
        if (ThumbnailsNeeded == null || ThumbnailSource == null) return;
        var missing = new List<int>();
        int top = ScrollY, bottom = ScrollY + ClientSize.Height, ahead = bottom + ClientSize.Height;
        foreach (var row in rows)
        {
            if (row.Count < 0 || row.Y + row.Height < top) continue;
            if (row.Y > ahead) break;
            for (int i = row.First; i < row.First + row.Count; i++)
            {
                int id = entries[i].Display.Id;
                if (ThumbnailSource(id) == null) missing.Add(id);
            }
        }
        ThumbnailsNeeded(missing);
    }

    // ---- hit testing & input ------------------------------------------------------------

    /// <summary>Returns the tile index under a client point, or -1. headerGroup is set for header hits.</summary>
    int HitTest(Point p, out int headerGroup)
    {
        headerGroup = -1;
        int y = p.Y + ScrollY;
        foreach (var row in rows)
        {
            if (y < row.Y) break;
            if (y >= row.Y + row.Height) continue;
            if (row.Count < 0) { headerGroup = row.Group; return -1; }
            for (int i = row.First; i < row.First + row.Count; i++)
                if (p.X >= tiles[i].X && p.X < tiles[i].Right) return i;
            return -1;
        }
        return -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        keyboardFocus = false;
        Focus();
        int i = HitTest(e.Location, out int header);
        bool ctrl = (ModifierKeys & Keys.Control) != 0, shift = (ModifierKeys & Keys.Shift) != 0;

        if (header >= 0 && e.Button == MouseButtons.Left)
        {
            var group = groups[header];
            var keys = entries.Skip(group.Start).Take(group.Count).Select(x => x.Key).ToList();
            bool all = keys.All(selected.Contains);
            if (!ctrl && !all) selected.Clear();
            foreach (var k in keys) { if (all) selected.Remove(k); else selected.Add(k); }
        }
        else if (i < 0)
        {
            if (!ctrl && !shift) selected.Clear();
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (!selected.Contains(entries[i].Key)) { selected.Clear(); selected.Add(entries[i].Key); }
            focus = anchor = i;
        }
        else if (shift && anchor >= 0)
        {
            if (!ctrl) selected.Clear();
            for (int k = Math.Min(anchor, i); k <= Math.Max(anchor, i); k++) selected.Add(entries[k].Key);
            focus = i;
        }
        else if (ctrl)
        {
            if (!selected.Remove(entries[i].Key)) selected.Add(entries[i].Key);
            focus = anchor = i;
        }
        else
        {
            selected.Clear();
            selected.Add(entries[i].Key);
            focus = anchor = i;
        }
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int i = HitTest(e.Location, out _);
        if (i >= 0 && e.Button == MouseButtons.Left) EntryActivated?.Invoke(entries[i]);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = HitTest(e.Location, out int header);
        if (i == hover && header == hoverHeader) return;
        InvalidateTile(hover);
        hover = i;
        hoverHeader = header;
        hoverStart = DateTime.UtcNow;
        if (i >= 0 && !Theme.ReducedMotion) clock.Start();
        Cursor = header >= 0 ? Cursors.Hand : Cursors.Default;
        tip.SetToolTip(this, i >= 0 ? Describe(entries[i]) : null);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = hoverHeader = -1;
        Invalidate();
    }

    static string Describe(MediaEntry e)
    {
        var lines = new List<string>
        {
            e.Primary.Name,
            e.Date == DateTime.MinValue ? "Date unknown" : e.Date.ToString("dddd d MMMM yyyy, HH:mm"),
            $"{FormatSize(e.Primary.Size)}  ·  phone folder {e.Primary.Folder}",
        };
        foreach (var (file, role) in e.Extras)
        {
            var what = role switch
            {
                FileRole.Edited => "edited version",
                FileRole.Live => "Live Photo motion",
                FileRole.EditedLive => "edited Live Photo motion",
                _ => "edit data",
            };
            lines.Add($"+ {file.Name}  ({what}, {FormatSize(file.Size)})");
        }
        return string.Join("\n", lines);
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 0 => "?",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.0} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:0.00} GB",
    };

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (entries.Count == 0) return;
        keyboardFocus = true;
        if (e.Control && e.KeyCode == Keys.A) { SelectAll(); e.Handled = true; return; }
        if (e.KeyCode == Keys.Enter && focus >= 0) { EntryActivated?.Invoke(entries[focus]); e.Handled = true; return; }
        if (e.KeyCode == Keys.Escape) { ClearSelection(); return; }
        if (e.KeyCode == Keys.Delete && selected.Count > 0) { DeleteRequested?.Invoke(); e.Handled = true; return; }
        if (e.KeyCode == Keys.Space && focus >= 0)
        {
            if (!selected.Remove(entries[focus].Key)) selected.Add(entries[focus].Key);
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        int cur = focus < 0 ? 0 : focus;
        int pageRows = Math.Max(1, ClientSize.Height / (TileH + Gap));
        int target = e.KeyCode switch
        {
            Keys.Left => cur - 1,
            Keys.Right => cur + 1,
            Keys.Up => Vertical(cur, -1),
            Keys.Down => Vertical(cur, +1),
            Keys.PageUp => Enumerable.Range(0, pageRows).Aggregate(cur, (t, _) => Vertical(t, -1)),
            Keys.PageDown => Enumerable.Range(0, pageRows).Aggregate(cur, (t, _) => Vertical(t, +1)),
            Keys.Home => 0,
            Keys.End => entries.Count - 1,
            _ => -2,
        };
        if (target == -2) { Invalidate(); return; }
        target = Math.Clamp(target, 0, entries.Count - 1);
        e.Handled = true;

        if (e.Shift)
        {
            if (anchor < 0) anchor = cur;
            selected.Clear();
            for (int k = Math.Min(anchor, target); k <= Math.Max(anchor, target); k++) selected.Add(entries[k].Key);
        }
        else if (!e.Control)
        {
            selected.Clear();
            selected.Add(entries[target].Key);
            anchor = target;
        }
        focus = target;
        EnsureVisible(target);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Index of the tile in the same column of the next/previous tile row (rows differ in length across months).</summary>
    int Vertical(int index, int dir)
    {
        int r = rows.FindIndex(x => x.Count >= 0 && index >= x.First && index < x.First + x.Count);
        if (r < 0) return index;
        int col = index - rows[r].First;
        for (int k = r + dir; k >= 0 && k < rows.Count; k += dir)
            if (rows[k].Count >= 0)
                return rows[k].First + Math.Min(col, rows[k].Count - 1);
        return index;
    }

    void EnsureVisible(int i)
    {
        if (i < 0 || i >= tiles.Length) return;
        var r = tiles[i];
        int y = ScrollY;
        if (r.Y - S(8) < y) y = r.Y - S(8);
        else if (r.Bottom + S(8) > y + ClientSize.Height) y = r.Bottom + S(8) - ClientSize.Height;
        // Keep the month header in view when moving onto a month's first row.
        int idx = rows.FindIndex(x => x.Count >= 0 && i >= x.First && i < x.First + x.Count);
        if (idx > 0 && rows[idx - 1].Count < 0 && rows[idx - 1].Y < y && r.Bottom - rows[idx - 1].Y <= ClientSize.Height)
            y = rows[idx - 1].Y;
        AutoScrollPosition = new Point(0, Math.Max(0, y));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { tip.Dispose(); clock.Dispose(); }
        base.Dispose(disposing);
    }
}
