using System.Diagnostics;
using PhoneExplorer.Hud;

namespace PhoneExplorer;

public sealed class MainForm : Form
{
    /// <summary>Tree row key; Year 0 = everything, Month 0 = the whole year.</summary>
    readonly record struct Period(int Year, int Month)
    {
        public static readonly Period New = new(-1, 0);
    }

    const int ThumbCacheLimit = 1500;
    const int WM_DEVICECHANGE = 0x0219;

    // Loads first: the theme must be in place before any control below is created.
    readonly Settings settings = LoadSettings();
    readonly PhoneWorker worker = new();

    readonly ToolStrip tools = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(Theme.Space3, Theme.Space1, Theme.Space3, Theme.Space1), Renderer = new HudRenderer() };
    readonly ToolStripButton refreshButton = HudRenderer.Style(new ToolStripButton("Refresh") { ToolTipText = "Read the phone again (F5)" }, ItemRole.Ghost);
    readonly ToolStripButton photosButton = HudRenderer.Style(new ToolStripButton("Photos") { CheckOnClick = true, ToolTipText = "HEIC and JPG photos" }, ItemRole.Toggle);
    readonly ToolStripButton videosButton = HudRenderer.Style(new ToolStripButton("Videos") { CheckOnClick = true, ToolTipText = "MOV and MP4 videos" }, ItemRole.Toggle);
    readonly ToolStripButton imagesButton = HudRenderer.Style(new ToolStripButton("Screenshots") { CheckOnClick = true, ToolTipText = "Screenshots, and PNG, WEBP and GIF files such as saved images" }, ItemRole.Toggle);
    readonly ToolStripButton otherButton = HudRenderer.Style(new ToolStripButton("Other") { CheckOnClick = true, ToolTipText = "Files that are not photos or videos, for example .DAT" }, ItemRole.Toggle);
    readonly HudTextBox searchBox = new(Theme.ColorBgElevated) { Width = 180 };
    ToolStripControlHost searchHost = null!;
    readonly ToolStripButton[] sizeButtons =
    {
        HudRenderer.Style(new ToolStripButton("S") { ToolTipText = "Small thumbnails" }, ItemRole.Segment),
        HudRenderer.Style(new ToolStripButton("M") { ToolTipText = "Medium thumbnails" }, ItemRole.Segment),
        HudRenderer.Style(new ToolStripButton("L") { ToolTipText = "Large thumbnails" }, ItemRole.Segment),
    };
    static readonly int[] SizeSteps = { 100, 150, 230 };
    readonly ToolStripButton copySelectedButton = HudRenderer.Style(new ToolStripButton("Copy selected") { ToolTipText = "Copy the selected items to this PC" }, ItemRole.Primary);
    readonly ToolStripButton moveButton = HudRenderer.Style(new ToolStripButton("Move to PC") { ToolTipText = "Cut: copy the selected items to this PC, then remove them from the phone (Ctrl+X)" }, ItemRole.Secondary);
    readonly ToolStripButton deleteButton =HudRenderer.Style(new ToolStripButton("Delete") { ToolTipText = "Delete the selected items from the phone (Del). Asks first." }, ItemRole.Danger);

    readonly DevicePanel devicePanel = new() { Dock = DockStyle.Top };
    readonly LibraryTree tree = new() { Dock = DockStyle.Fill };
    readonly ThumbGrid grid = new() { Dock = DockStyle.Fill };
    readonly SplitContainer split = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = 5 };
    readonly ContextMenuStrip gridMenu = new() { Renderer = new HudRenderer() };
    readonly ContextMenuStrip deviceMenu = new() { Renderer = new HudRenderer() };
    readonly ContextMenuStrip treeMenu = new() { Renderer = new HudRenderer() };
    readonly Panel libraryLabel = new() { Dock = DockStyle.Top };
    readonly ToolTip treeTip = HudToolTip.Create();

    readonly StatusStrip status = new() { Renderer = new HudRenderer(), SizingGrip = false, Padding = new Padding(Theme.Space2, 0, Theme.Space2, 0) };
    readonly ToolStripStatusLabel statusLabel = HudRenderer.Style(new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft }, ItemRole.Text);
    readonly HudProgressItem progress = new() { Visible = false };
    readonly ToolStripButton cancelButton = HudRenderer.Style(new ToolStripButton("Cancel") { Visible = false }, ItemRole.Ghost);
    readonly HudReadoutItem readout = new();
    readonly ToolStripDropDownButton themeButton = HudRenderer.Style(new ToolStripDropDownButton("Theme") { ToolTipText = "Change how Phone Explorer looks" }, ItemRole.Ghost);

    readonly System.Windows.Forms.Timer deviceTimer = new() { Interval = 1500 };

    readonly Dictionary<int, Bitmap> thumbs = new();
    readonly LinkedList<int> thumbOrder = new();
    readonly HashSet<int> noThumb = new();

    List<MediaFile> files = new();
    List<MediaEntry> library = new();
    /// <summary>The phone on screen, and every phone connected (for the picker).</summary>
    PhoneDevice? device;
    List<PhoneDevice> devices = new();
    /// <summary>The phone the user picked; null means the first one found.</summary>
    string? selectedId;
    SeenStore? seen;
    string? seenDevice;
    bool scanning, busy, pendingRescan;
    /// <summary>A newer release waiting to be offered once the current copy, move or delete finishes.</summary>
    Updater.Release? pendingUpdate;

    public MainForm()
    {
        Text = $"Phone Explorer {Updater.Current.ToString(3)}";
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(1280, 820);
        MinimumSize = new Size(760, 480);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        BuildToolbar();
        BuildMenu();
        BuildSidebar();
        BuildThemeMenu();

        split.Paint += (_, e) =>
        {
            // The splitter is a single hairline.
            var r = split.SplitterRectangle;
            using var pen = new Pen(Theme.SolidBorder);
            e.Graphics.DrawLine(pen, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Bottom);
        };
        split.Panel2.Controls.Add(grid);
        status.Items.AddRange(new ToolStripItem[] { statusLabel, progress, cancelButton, readout, themeButton });
        Controls.Add(split);
        Controls.Add(tools);
        Controls.Add(status);

        grid.ThumbSize = settings.ThumbSize;
        grid.ThumbnailSource = GetThumb;
        grid.ThumbnailsNeeded += ids => worker.SetWantedThumbnails(ids.Where(id => !noThumb.Contains(id)).ToList(), grid.ThumbPixels);
        grid.SelectionChanged += (_, _) => UpdateSelectionInfo();
        grid.EntryActivated += Open;
        grid.DeleteRequested += () => DeleteEntries(grid.SelectedEntries);
        grid.ContextMenuStrip = gridMenu;
        grid.IsNew = IsNew;
        worker.ThumbnailReady += (id, bmp) => UI(() => ThumbArrived(id, bmp));

        cancelButton.Click += (_, _) => { worker.CancelRequested = true; cancelButton.Enabled = false; statusLabel.Text = "Cancelling…"; };
        deviceTimer.Tick += (_, _) => { deviceTimer.Stop(); CheckDevice(); };

        Shown += (_, _) =>
        {
            split.SplitterDistance = LogicalToDeviceUnits(240);
            devicePanel.Boot();
            StartScan();
            Updater.CleanUp();
            CheckForUpdate();
        };
        FormClosing += OnClosing;
        ApplyTheme();
        UpdateButtons();
    }

    static Settings LoadSettings()
    {
        var s = Settings.Load();
        Theme.Use(s.ThemeName);
        return s;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HudWindow.Apply(this);
    }

    // ---- layout -------------------------------------------------------------------------

    void BuildToolbar()
    {
        photosButton.Checked = settings.ShowPhotos;
        videosButton.Checked = settings.ShowVideos;
        imagesButton.Checked = settings.ShowImages;
        otherButton.Checked = settings.ShowOther;
        foreach (var b in new[] { photosButton, videosButton, imagesButton, otherButton })
            b.CheckedChanged += (_, _) =>
            {
                settings.ShowPhotos = photosButton.Checked;
                settings.ShowVideos = videosButton.Checked;
                settings.ShowImages = imagesButton.Checked;
                settings.ShowOther = otherButton.Checked;
                RebuildTree();
                ApplyView(keepScroll: false);
            };

        int current = settings.ThumbSize <= 110 ? 0 : settings.ThumbSize >= 210 ? 2 : 1;
        for (int i = 0; i < sizeButtons.Length; i++)
        {
            int index = i;
            sizeButtons[i].Checked = i == current;
            sizeButtons[i].Click += (_, _) =>
            {
                for (int k = 0; k < sizeButtons.Length; k++) sizeButtons[k].Checked = k == index;
                settings.ThumbSize = SizeSteps[index];
                ClearThumbs();
                grid.ThumbSize = settings.ThumbSize;
            };
        }

        searchBox.Box.PlaceholderText = "file name";
        searchBox.Box.TextChanged += (_, _) => ApplyView(keepScroll: false);
        searchHost = new ToolStripControlHost(searchBox) { AutoSize = false, Width = searchBox.Width, Margin = new Padding(0, 3, Theme.Space2, 3) };

        refreshButton.Click += (_, _) => StartScan();
        copySelectedButton.Click += (_, _) => CopyEntries(grid.SelectedEntries);
        deleteButton.Click += (_, _) => DeleteEntries(grid.SelectedEntries);
        moveButton.Click += (_, _) => MoveEntries(grid.SelectedEntries);
        copySelectedButton.Alignment = moveButton.Alignment = deleteButton.Alignment = ToolStripItemAlignment.Right;

        tools.Items.AddRange(new ToolStripItem[]
        {
            refreshButton, new ToolStripSeparator(),
            HudRenderer.Style(new ToolStripLabel("Show"), ItemRole.Label), photosButton, videosButton, imagesButton, otherButton, new ToolStripSeparator(),
            HudRenderer.Style(new ToolStripLabel("Find"), ItemRole.Label), searchHost, new ToolStripSeparator(),
            HudRenderer.Style(new ToolStripLabel("Size"), ItemRole.Label), sizeButtons[0], sizeButtons[1], sizeButtons[2],
            copySelectedButton, moveButton, deleteButton,
        });
    }

    void BuildSidebar()
    {
        devicePanel.Click += (_, _) => ShowDeviceMenu();
        var label = libraryLabel;
        label.Paint += (_, e) =>
        {
            // Section label over a hairline; soft themes add a sparkle by it (docs/06-iconography.md).
            var g = e.Graphics;
            int x = LogicalToDeviceUnits(Theme.Hud ? Theme.Space3 : Theme.Space5), y = label.Height - LogicalToDeviceUnits(Theme.Space4) - (Theme.Hud ? 0 : LogicalToDeviceUnits(Theme.Space1));
            var font = Theme.Label();
            int w = Theme.DrawTracked(g, Theme.Case("Library"), font, Theme.ColorEyebrow, new Point(x, y), Theme.TrackingLabel);
            if (!Theme.Hud)
            {
                int d = LogicalToDeviceUnits(Theme.Space3);
                int h = TextRenderer.MeasureText(g, "X", font, Size.Empty, TextFormatFlags.NoPadding).Height;
                Theme.DrawSparkle(g, new Rectangle(x + w + LogicalToDeviceUnits(Theme.Space1), y + (h - d) / 2, d, d), Theme.ColorAccent);
            }
            using var pen = new Pen(Theme.Over(Theme.ColorDivider, Theme.ColorBg));
            g.DrawLine(pen, x, label.Height - 1, label.Width - x, label.Height - 1);
        };
        tree.AfterSelect += (_, _) => ApplyView(keepScroll: false);
        tree.NodeMouseHover += (_, e) => treeTip.SetToolTip(tree, e.Node?.Tag is TreeRow { Key: Period p } && p == Period.New
            ? "Items that appeared on the phone since the app last saw it,\nincluding photos you have just unhidden.\nRight-click to mark everything as seen."
            : null);
        split.Panel1.Controls.Add(tree);
        split.Panel1.Controls.Add(label);
        split.Panel1.Controls.Add(devicePanel);
    }

    void BuildMenu()
    {
        ToolStripMenuItem Item(string text, Action action) => HudRenderer.Style(new ToolStripMenuItem(text, null, (_, _) => action()), ItemRole.Ghost);
        var open = Item("Open", () => { var s = grid.SelectedEntries; if (s.Count > 0) Open(s[0]); });
        var copy = Item("Copy selected", () => CopyEntries(grid.SelectedEntries));
        copy.ShortcutKeyDisplayString = "Ctrl+C";
        var move = Item("Move to PC", () => MoveEntries(grid.SelectedEntries));
        move.ShortcutKeyDisplayString = "Ctrl+X";
        var markSeen = Item("Mark everything as seen", MarkAllSeen);
        var delete = HudRenderer.Style(new ToolStripMenuItem("Delete from phone", null, (_, _) => DeleteEntries(grid.SelectedEntries)) { ShortcutKeyDisplayString = "Del" }, ItemRole.Danger);
        gridMenu.Items.AddRange(new ToolStripItem[]
        {
            open, copy, move, Item("Copy all shown", () => CopyEntries(grid.Entries.ToList())), new ToolStripSeparator(),
            Item("Select all shown", grid.SelectAll), Item("Clear selection", grid.ClearSelection), new ToolStripSeparator(),
            markSeen, new ToolStripSeparator(),
            delete,
        });
        gridMenu.Opening += (_, _) =>
        {
            bool any = grid.SelectedEntries.Count > 0;
            open.Enabled = copy.Enabled = any && !busy;
            delete.Enabled = move.Enabled = any && !busy && !scanning;
            markSeen.Enabled = library.Any(IsNew);
        };

        var treeMark = Item("Mark everything as seen", MarkAllSeen);
        treeMenu.Items.Add(treeMark);
        treeMenu.Opening += (_, _) => treeMark.Enabled = library.Any(IsNew);
        tree.ContextMenuStrip = treeMenu;
    }

    // ---- theme --------------------------------------------------------------------------

    void BuildThemeMenu()
    {
        themeButton.DropDown.Renderer = new HudRenderer();
        foreach (var def in Theme.All)
        {
            var item = HudRenderer.Style(new ToolStripMenuItem(def.DisplayName, null, (_, _) => SwitchTheme(def)) { Tag = def }, ItemRole.Ghost);
            themeButton.DropDownItems.Add(item);
        }
    }

    void SwitchTheme(ThemeDef def)
    {
        if (!Theme.Use(def.Id)) return;
        settings.ThemeName = def.Id;
        settings.Save();
        SuspendLayout();
        ApplyTheme();
        ResumeLayout(true);
    }

    /// <summary>
    /// Re-applies the theme to everything that keeps a theme value (fonts, sizes, BackColor).
    /// Paint code reads the theme on every paint; dialogs are created fresh each time.
    /// </summary>
    void ApplyTheme()
    {
        BackColor = Theme.ColorBg;
        ForeColor = Theme.ColorText;
        Font = Theme.Body(Theme.TextMd);
        split.BackColor = split.Panel1.BackColor = split.Panel2.BackColor = Theme.ColorBg;
        libraryLabel.BackColor = Theme.ColorBg;
        libraryLabel.Height = LogicalToDeviceUnits(Theme.Hud ? Theme.Space8 : Theme.Space10);
        searchBox.ApplyTheme(Theme.ColorBgElevated);
        searchHost.Size = searchBox.Size;
        foreach (var strip in new ToolStrip[] { tools, status, gridMenu, treeMenu, deviceMenu })
            HudRenderer.Restyle(strip);
        themeButton.Text = Theme.Current.DisplayName;
        HudRenderer.Style(themeButton, ItemRole.Ghost);
        foreach (ToolStripMenuItem item in themeButton.DropDownItems)
            item.Checked = item.Tag == Theme.Current;
        devicePanel.ApplyTheme();
        tree.ApplyTheme();
        grid.ApplyTheme();
        if (IsHandleCreated) HudWindow.Apply(this);
        UpdateSelectionInfo();
        Invalidate(true);
    }

    bool IsNew(MediaEntry e) => seen != null && e.AllFiles.Any(seen.IsNew);

    /// <summary>Clears the "new" view: everything currently on the phone becomes the new baseline.</summary>
    void MarkAllSeen()
    {
        if (seen == null || scanning) return;
        seen.Remember(files, resetBaseline: true);
        RebuildTree();
        ApplyView(keepScroll: false);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { searchBox.Box.Focus(); searchBox.Box.SelectAll(); return true; }
        if (keyData == Keys.F5) { StartScan(); return true; }
        // Copy and cut act on the photo selection only when the grid has focus (not the Find box).
        if (grid.Focused && keyData == (Keys.Control | Keys.C)) { CopyEntries(grid.SelectedEntries); return true; }
        if (grid.Focused && keyData == (Keys.Control | Keys.X)) { MoveEntries(grid.SelectedEntries); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void UI(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }

    // ---- device and scanning ------------------------------------------------------------

    protected override void WndProc(ref Message m)
    {
        // Something was plugged in or out: check for the phone shortly after things settle.
        if (m.Msg == WM_DEVICECHANGE) { deviceTimer.Stop(); deviceTimer.Start(); }
        base.WndProc(ref m);
    }

    void CheckDevice()
    {
        if (scanning || busy) { pendingRescan = true; return; }
        worker.ListDevices(list => UI(() =>
        {
            SetDevices(list);
            if (scanning || busy) { pendingRescan = true; return; }
            if (device != null && !list.Any(d => d.Id == device.Id))
            {
                // The phone on screen was unplugged: show another one if there is one.
                selectedId = null;
                if (list.Count > 0) StartScan();
                else ShowNoDevice("Phone disconnected");
            }
            else if (list.Count > 0 && (device == null || files.Count == 0)) StartScan();
        }));
    }

    void SetDevices(List<PhoneDevice> list)
    {
        devices = list;
        devicePanel.Switchable = devices.Count > 1;
    }

    /// <summary>The device picker: lists the connected phones when there is more than one.</summary>
    void ShowDeviceMenu()
    {
        if (devices.Count < 2 || scanning || busy) return;
        deviceMenu.Items.Clear();
        foreach (var d in devices)
        {
            var item = HudRenderer.Style(new ToolStripMenuItem(d.Name, null, (_, _) =>
            {
                if (d.Id == device?.Id) return;
                selectedId = d.Id;
                StartScan();
            }) { Checked = d.Id == device?.Id }, ItemRole.Ghost);
            deviceMenu.Items.Add(item);
        }
        deviceMenu.Show(devicePanel, new Point(LogicalToDeviceUnits(Theme.Space2), devicePanel.Height - LogicalToDeviceUnits(Theme.Space2)));
    }

    void StartScan()
    {
        if (scanning || busy) return;
        scanning = true;
        pendingRescan = false;
        files = new();
        library = new();
        ClearThumbs();
        grid.EmptyText = Theme.Say("Searching\nLooking for a connected phone.", "Just a moment…\nLooking for your phone.");
        devicePanel.Set(DeviceState.Searching, device?.Name ?? "", "", "Looking for a connected phone");
        RebuildTree();
        ApplyView(keepScroll: false);
        statusLabel.Text = "Looking for your phone…";
        progress.SetIndeterminate();
        progress.Visible = true;
        UpdateButtons();

        var lastRefresh = Stopwatch.StartNew();
        worker.Scan(selectedId,
            d => UI(() => device = d),
            text => UI(() => statusLabel.Text = text),
            batch => UI(() =>
            {
                files.AddRange(batch);
                // Rebuilding is cheap, but keep the view calm while lots of small folders stream in.
                if (lastRefresh.ElapsedMilliseconds > 400 || files.Count == batch.Count)
                {
                    lastRefresh.Restart();
                    Rebuild(keepScroll: true);
                }
            }),
            (found, error) => UI(() => ScanFinished(found, error)));
        worker.ListDevices(list => UI(() => SetDevices(list)));
    }

    void ScanFinished(PhoneDevice? found, string? error)
    {
        scanning = false;
        progress.Visible = false;
        device = found;
        if (found == null)
        {
            ShowNoDevice("No phone found");
        }
        else if (error != null)
        {
            Rebuild(keepScroll: true);
            grid.EmptyText = "Can't read the phone\n" + error;
            devicePanel.Set(DeviceState.Error, found.Name, "", error.Split('\n')[0]);
            statusLabel.Text = error.Split('\n')[0];
        }
        else
        {
            if (seen == null || seenDevice != found.Name) { seen = new SeenStore(found.Name); seenDevice = found.Name; }
            seen.Remember(files);
            grid.EmptyText = Theme.Say("Nothing on the phone\nPhotos and videos on the phone appear here.", "Nothing here yet\nPhotos and videos on the phone will show up here ♥");
            Rebuild(keepScroll: true);
            statusLabel.Text = "Ready";
        }
        UpdateButtons();
        if (pendingRescan) CheckDevice();
    }

    void ShowNoDevice(string headline)
    {
        device = null;
        files = new();
        library = new();
        ClearThumbs();
        RebuildTree();
        ApplyView(keepScroll: false);
        grid.EmptyText = headline + "\nConnect a phone with a cable and unlock it. On an iPhone, tap Trust. On Android, tap the USB notification and choose File transfer. " +
                         "It's picked up automatically, or press Refresh (F5).";
        devicePanel.Set(DeviceState.NotFound, "", "", "Connect a phone with a cable");
        statusLabel.Text = headline;
        UpdateButtons();
    }

    void Rebuild(bool keepScroll)
    {
        library = Library.Build(files, device?.IsApple ?? true);
        if (device != null || scanning)
        {
            int count = library.Count(e => e.Kind != MediaKind.Other);
            var detail = $"{ThumbGrid.FormatSize(files.Sum(f => Math.Max(0, f.Size)))}  ·  {files.Select(f => f.Folder).Distinct().Count():N0} folders";
            devicePanel.Set(scanning ? DeviceState.Searching : DeviceState.Connected, device?.Name ?? "", count.ToString("N0"), detail);
        }
        RebuildTree();
        ApplyView(keepScroll);
    }

    // ---- filtering and the month tree ---------------------------------------------------

    bool KindShown(MediaKind k) => k switch
    {
        MediaKind.Photo => photosButton.Checked,
        MediaKind.Video => videosButton.Checked,
        MediaKind.Image => imagesButton.Checked,
        _ => otherButton.Checked,
    };

    bool InPeriod(MediaEntry e, Period p) =>
        p == Period.New ? IsNew(e) :
        p.Year == 0 || (e.Date.Year == p.Year && (p.Month == 0 || e.Date.Month == p.Month));

    Period SelectedPeriod => tree.SelectedNode?.Tag is TreeRow { Key: Period p } ? p : new Period(0, 0);

    void RebuildTree()
    {
        var current = SelectedPeriod;
        var shown = library.Where(e => KindShown(e.Kind)).ToList();

        TreeNode Add(TreeNodeCollection parent, Period key, string label, int count, bool info = false)
        {
            var node = parent.Add(label);
            node.Tag = new TreeRow(key, label, count, info);
            return node;
        }

        tree.BeginUpdate();
        tree.Nodes.Clear();
        TreeNode select = Add(tree.Nodes, new Period(0, 0), "Everything", shown.Count);
        // Always present (even mid-scan) so choosing it and pressing F5 keeps you on it.
        int newCount = shown.Count(IsNew);
        var newNode = Add(tree.Nodes, Period.New, "New since last time", newCount, info: newCount > 0);
        if (current == Period.New) select = newNode;
        foreach (var year in shown.GroupBy(e => e.Date.Year).OrderByDescending(g => g.Key))
        {
            var yearNode = Add(tree.Nodes, new Period(year.Key, 0), year.Key == 1 ? "Unknown date" : year.Key.ToString(), year.Count());
            if (current == new Period(year.Key, 0)) select = yearNode;
            foreach (var month in year.GroupBy(e => e.Date.Month).OrderByDescending(g => g.Key))
            {
                var monthNode = Add(yearNode.Nodes, new Period(year.Key, month.Key), new DateTime(2000, month.Key, 1).ToString("MMMM"), month.Count());
                if (current == new Period(year.Key, month.Key)) select = monthNode;
            }
        }
        // Expand the most recent year so recent months are one click away.
        tree.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Tag is TreeRow { Key: Period { Year: > 0 } })?.Expand();
        select.EnsureVisible();
        tree.SelectedNode = select;
        tree.EndUpdate();
    }

    void ApplyView(bool keepScroll)
    {
        var period = SelectedPeriod;
        var search = searchBox.Text.Trim();
        var list = library
            .Where(e => KindShown(e.Kind) && InPeriod(e, period) &&
                        (search.Length == 0 || e.AllFiles.Any(f => f.Name.Contains(search, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Primary.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = new List<EntryGroup>();
        for (int i = 0; i < list.Count;)
        {
            var d = list[i].Date;
            int start = i;
            while (i < list.Count && list[i].Date.Year == d.Year && list[i].Date.Month == d.Month) i++;
            groups.Add(new EntryGroup(d == DateTime.MinValue ? "Unknown date" : Library.MonthTitle(d.Year, d.Month), start, i - start));
        }
        if (device != null && !scanning && library.Count > 0)
            grid.EmptyText = period == Period.New
                ? device.IsApple
                    ? "Nothing new\nPhotos you take or unhide on the phone appear here after a Refresh (F5). " +
                      "Hidden photos are never shared over USB, so unhide them on the phone first."
                    : "Nothing new\nPhotos you take on the phone appear here after a Refresh (F5)."
                : "Nothing matches\nNo items match the current filters. Turn on more types under Show, or clear Find.";
        grid.SetEntries(list, groups, keepScroll);
        UpdateSelectionInfo();
    }

    void UpdateSelectionInfo()
    {
        var sel = grid.SelectedEntries;
        if (sel.Count == 0) readout.Set((grid.Entries.Count.ToString("N0"), "shown"));
        else readout.Set((sel.Count.ToString("N0"), "selected"), (ThumbGrid.FormatSize(sel.Sum(e => e.Primary.Size)), ""));
        UpdateButtons();
    }

    void UpdateButtons()
    {
        refreshButton.Enabled = !scanning && !busy;
        copySelectedButton.Enabled = !busy && grid.SelectedEntries.Count > 0;
        deleteButton.Enabled = moveButton.Enabled = !busy && !scanning && grid.SelectedEntries.Count > 0;
    }

    // ---- thumbnails ---------------------------------------------------------------------

    Bitmap? GetThumb(int id) => thumbs.TryGetValue(id, out var bmp) ? bmp : null;

    void ThumbArrived(int id, Bitmap? bmp)
    {
        if (bmp == null) { noThumb.Add(id); return; }
        if (!files.Any(f => f.Id == id) || thumbs.ContainsKey(id)) { bmp.Dispose(); return; }
        thumbs[id] = bmp;
        thumbOrder.AddLast(id);
        while (thumbOrder.Count > ThumbCacheLimit)
        {
            var old = thumbOrder.First!.Value;
            thumbOrder.RemoveFirst();
            thumbs.Remove(old, out var gone);
            gone?.Dispose();
        }
        grid.Invalidate();
    }

    void ClearThumbs()
    {
        worker.SetWantedThumbnails(new(), grid.ThumbPixels);
        foreach (var b in thumbs.Values) b.Dispose();
        thumbs.Clear();
        thumbOrder.Clear();
        noThumb.Clear();
    }

    // ---- open and copy ------------------------------------------------------------------

    static readonly (string, ButtonKind, DialogResult) CloseButton = ("Close", ButtonKind.Ghost, DialogResult.Cancel);

    void Open(MediaEntry entry)
    {
        if (busy) return;
        var file = entry.Display;
        statusLabel.Text = $"Opening {file.Name}…";
        worker.CopyToTemp(file, (path, error) => UI(() =>
        {
            statusLabel.Text = "Ready";
            if (path == null)
            {
                HudDialog.Show(this, "Can't open file", $"{file.Name} couldn't be copied from the phone: {error}\nCheck the phone is still connected and unlocked, then try again.", CloseButton);
                return;
            }
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception ex)
            {
                HudDialog.Show(this, "Can't open file", $"Windows couldn't open {file.Name}: {ex.Message}\n" +
                    "HEIC photos need the free HEIF Image Extensions from the Microsoft Store.", CloseButton);
            }
        }));
    }

    static string Plural(int n, string one) => n == 1 ? $"1 {one}" : $"{n:N0} {one}s";

    void BeginWork(int total)
    {
        busy = true;
        worker.CancelRequested = false;
        progress.SetProgress(0, total);
        progress.Visible = cancelButton.Visible = cancelButton.Enabled = true;
        UpdateButtons();
    }

    void EndWork()
    {
        busy = false;
        progress.Visible = cancelButton.Visible = false;
        statusLabel.Text = "Ready";
        UpdateButtons();
        if (pendingUpdate != null) BeginInvoke(OfferUpdate);
    }

    // ---- updates -------------------------------------------------------------------------

    async void CheckForUpdate()
    {
        pendingUpdate = await Updater.CheckAsync();
        if (pendingUpdate != null && !busy && !IsDisposed) OfferUpdate();
    }

    /// <summary>Asks once per session; never while a copy, move or delete is running.</summary>
    async void OfferUpdate()
    {
        var release = pendingUpdate;
        if (release == null || busy || IsDisposed) return;
        pendingUpdate = null;
        if (HudDialog.Show(this, "Update available",
                $"Phone Explorer {release.Version.ToString(3)} is available. You have {Updater.Current.ToString(3)}.\n\n" +
                "Updating takes a moment: the app downloads the new version, closes, and opens again. Nothing on your phone or PC is changed.",
                ("Update now", ButtonKind.Primary, DialogResult.Yes), ("Later", ButtonKind.Ghost, DialogResult.No)) != DialogResult.Yes)
            return;

        busy = true;
        UpdateButtons();
        settings.Save();
        statusLabel.Text = $"Downloading Phone Explorer {release.Version.ToString(3)}…";
        progress.SetProgress(0, 1);
        progress.Visible = true;
        var error = await Updater.InstallAsync(release, new Progress<(long Done, long Total)>(p =>
            progress.SetProgress((int)(p.Done / 1024), (int)Math.Max(1, p.Total / 1024))));
        if (error == null)
        {
            // The new copy is starting: quit without the "work in progress" question.
            busy = false;
            Close();
            return;
        }
        EndWork();
        HudDialog.Show(this, "Couldn't update",
            $"The update didn't install: {error}\nThis version still works. You can also download the new version from {Updater.ReleasesPage}",
            CloseButton);
    }

    Action<int, int, string> Progress(string verb) => (i, total, name) => UI(() =>
    {
        progress.SetProgress(i, total);
        statusLabel.Text = $"{verb} {name}";
    });

    void OpenFolderPrompt(string heading, string message, string destination)
    {
        var answer = HudDialog.Show(this, heading, message,
            ("Open folder", ButtonKind.Primary, DialogResult.Yes), ("Close", ButtonKind.Ghost, DialogResult.No));
        if (answer == DialogResult.Yes && Directory.Exists(destination))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{destination}\"") { UseShellExecute = true });
    }

    static string FailureList(List<string> failed) =>
        string.Join("\n", failed.Take(8)) + (failed.Count > 8 ? "\n…" : "");

    List<CopyRequest> Requests(List<MediaEntry> entries, bool everything) => entries.SelectMany(e =>
    {
        var dir = settings.Organise && e.Date != DateTime.MinValue ? Path.Combine(e.Date.ToString("yyyy"), e.Date.ToString("MM MMMM")) : "";
        return e.AllFiles.Where(f =>
        {
            if (everything || f == e.Primary) return true;
            return e.Extras.First(x => x.File == f).Role switch
            {
                FileRole.Edited => settings.IncludeEdits,
                FileRole.Live => settings.IncludeLive,
                FileRole.EditedLive => settings.IncludeLive && settings.IncludeEdits,
                _ => settings.IncludeSidecars,
            };
        }).Select(f => new CopyRequest(f.Id, f.Name, dir, f.Size));
    }).ToList();

    // ---- copy ----------------------------------------------------------------------------

    void CopyEntries(List<MediaEntry> entries)
    {
        if (entries.Count == 0 || busy) return;
        using (var dialog = new CopyDialog(settings, entries.Count, entries.Sum(e => e.Primary.Size), device?.Name ?? "the phone", device?.IsApple ?? true))
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var requests = Requests(entries, everything: false);
        var destination = settings.Destination;
        BeginWork(requests.Count);
        worker.Copy(requests, destination, Progress("Copying"), r => UI(() =>
        {
            EndWork();
            var heading = r.Cancelled ? "Copy cancelled" : r.Failed.Count > 0 ? "Copy finished with errors" : Theme.Say("Copy complete", "Yay, all copied ♥");
            var msg = $"Copied {Plural(r.Copied, "file")} ({ThumbGrid.FormatSize(r.Bytes)}) to {destination}." +
                      (r.Skipped > 0 ? $"\nSkipped {r.Skipped:N0} already there." : "") +
                      (r.Failed.Count > 0 ? $"\n\n{r.Failed.Count:N0} failed. Check the phone is connected and unlocked, then copy again; finished files are skipped.\n" + FailureList(r.Failed) : "");
            OpenFolderPrompt(heading, msg, destination);
            if (pendingRescan) CheckDevice();
        }));
    }

    // ---- move (cut and paste): copy, verify, then delete from the phone -------------------

    void MoveEntries(List<MediaEntry> entries)
    {
        if (entries.Count == 0 || busy || scanning) return;
        using (var dialog = new CopyDialog(settings, entries.Count, entries.Sum(e => e.AllFiles.Sum(f => Math.Max(0, f.Size))), device?.Name ?? "the phone", device?.IsApple ?? true, move: true))
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // A move takes every file of each item, so nothing is left orphaned on the phone.
        var requests = Requests(entries, everything: true);
        var destination = settings.Destination;
        BeginWork(requests.Count);
        worker.Copy(requests, destination, Progress("Copying"), copy => UI(() =>
        {
            // Only items whose every file is verified on this PC are deleted from the phone.
            var safe = entries.Where(e => e.AllFiles.All(f => copy.Verified.Contains(f.Id))).ToList();
            int kept = entries.Count - safe.Count;
            if (copy.Cancelled || safe.Count == 0)
            {
                EndWork();
                var msg = (copy.Cancelled ? "The move was cancelled before anything was deleted from the phone. " : "Nothing was deleted from the phone because no item copied completely. ") +
                          $"Copied {Plural(copy.Copied, "file")} to {destination}." +
                          (copy.Failed.Count > 0 ? "\n\n" + FailureList(copy.Failed) : "");
                OpenFolderPrompt(copy.Cancelled ? "Move cancelled" : "Move failed", msg, destination);
                return;
            }
            var groups = safe.Select(e => e.AllFiles.ToList()).ToList();
            progress.SetProgress(0, groups.Sum(g => g.Count));
            worker.Delete(groups, Progress("Removing from phone"), del => UI(() =>
            {
                EndWork();
                RemoveDeleted(del.Deleted);
                int movedItems = safe.Count(e => e.AllFiles.All(f => del.Deleted.Contains(f.Id)));
                int notMoved = entries.Count - movedItems;
                var heading = del.Cancelled ? "Move stopped" : notMoved > 0 ? "Move finished with problems" : Theme.Say("Move complete", "All moved ♥");
                var msg = $"Moved {Plural(movedItems, "item")} to {destination}." +
                          (notMoved > 0
                              ? $"\n\n{Plural(notMoved, "item")} stayed on the phone. Anything copied is still on this PC, so it's safe to try again." +
                                "\n" + FailureList(copy.Failed.Concat(del.Failed).ToList())
                              : "");
                OpenFolderPrompt(heading, msg, destination);
                if (pendingRescan) CheckDevice();
            }));
        }));
    }

    // ---- delete --------------------------------------------------------------------------

    /// <summary>
    /// How many entries already have their original in the copy destination (either layout:
    /// sorted into year/month folders or flat), at the same size as on the phone.
    /// </summary>
    int AlreadyCopied(List<MediaEntry> entries)
    {
        var dest = settings.Destination;
        if (!Directory.Exists(dest)) return 0;
        return entries.Count(e => new[]
        {
            Path.Combine(dest, e.Date.ToString("yyyy"), e.Date.ToString("MM MMMM"), e.Primary.Name),
            Path.Combine(dest, e.Primary.Name),
        }.Any(p => File.Exists(p) && new FileInfo(p).Length == e.Primary.Size));
    }

    void DeleteEntries(List<MediaEntry> entries)
    {
        if (entries.Count == 0 || busy || scanning) return;
        int fileCount = entries.Sum(e => e.AllFiles.Count());
        long bytes = entries.Sum(e => e.AllFiles.Sum(f => Math.Max(0, f.Size)));
        int copied = AlreadyCopied(entries);
        var what = entries.Count == 1 ? entries[0].Primary.Name : $"{entries.Count:N0} items";
        var backup = copied == entries.Count
            ? $"All of them are already in your copy folder ({settings.Destination})."
            : copied == 0
                ? $"None of them are in your copy folder ({settings.Destination}). Copy them first, or use Move to PC, if you want to keep them."
                : $"Only {copied:N0} of {entries.Count:N0} are in your copy folder ({settings.Destination}). Copy the rest first if you want to keep them.";
        bool apple = device?.IsApple ?? true;
        var message = apple
            ? $"{what} ({Plural(fileCount, "file")}, {ThumbGrid.FormatSize(bytes)}) will be deleted from {device?.Name ?? "the iPhone"}, " +
              "including edited versions, Live Photo clips and edit data.\n\n" + backup + "\n\n" +
              "This can't be undone from this PC. If iCloud Photos is on, they're also deleted from iCloud and your other devices."
            : $"{what} ({Plural(fileCount, "file")}, {ThumbGrid.FormatSize(bytes)}) will be deleted from {device?.Name ?? "the phone"}.\n\n" + backup + "\n\n" +
              "This deletes them permanently: they don't go to the phone's Recycle bin, and it can't be undone from this PC. " +
              "Copies already backed up to Google Photos or another cloud service stay there.";
        if (HudDialog.Show(this, "Delete from phone", message,
                (entries.Count == 1 ? "Delete 1 item" : $"Delete {entries.Count:N0} items", ButtonKind.Danger, DialogResult.Yes),
                ("Cancel", ButtonKind.Ghost, DialogResult.Cancel)) != DialogResult.Yes)
            return;

        BeginWork(fileCount);
        worker.Delete(entries.Select(e => e.AllFiles.ToList()).ToList(), Progress("Deleting"), r => UI(() =>
        {
            EndWork();
            RemoveDeleted(r.Deleted);
            if (r.Failed.Count > 0 || r.Cancelled)
                HudDialog.Show(this, r.Cancelled ? "Delete stopped" : "Some items weren't deleted",
                    $"Deleted {Plural(r.Deleted.Count, "file")}." +
                    (r.Failed.Count > 0
                        ? $"\n\n{r.Failed.Count:N0} couldn't be deleted. The phone can refuse some items, for example ones synced from a computer; delete those on the phone instead.\n" + FailureList(r.Failed)
                        : ""),
                    ("Close", ButtonKind.Ghost, DialogResult.Cancel));
            else statusLabel.Text = $"Deleted {Plural(r.Deleted.Count, "file")} from the phone";
            if (pendingRescan) CheckDevice();
        }));
    }

    /// <summary>Drops deleted files from the library without rescanning the phone.</summary>
    void RemoveDeleted(List<int> deleted)
    {
        if (deleted.Count == 0) return;
        var gone = deleted.ToHashSet();
        files = files.Where(f => !gone.Contains(f.Id)).ToList();
        foreach (var id in gone)
            if (thumbs.Remove(id, out var b)) { thumbOrder.Remove(id); b.Dispose(); }
        Rebuild(keepScroll: true);
    }

    void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (busy && HudDialog.Show(this, "Work in progress",
                "Quitting stops the copy, move or delete that's running. Finished files stay done; the file in progress may be left incomplete on this PC.",
                ("Stop and quit", ButtonKind.Danger, DialogResult.Yes), ("Keep going", ButtonKind.Ghost, DialogResult.No)) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        worker.CancelRequested = true;
        settings.Save();
        worker.Dispose();
    }
}
