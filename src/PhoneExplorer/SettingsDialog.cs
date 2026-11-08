using PhoneExplorer.Hud;

namespace PhoneExplorer;

/// <summary>App settings: where the cache lives and how big it may grow. Applies the changes on OK.</summary>
public sealed class SettingsDialog : Form
{
    readonly AppCache cache;
    readonly HudTextBox location;
    readonly HudTextBox limit;
    readonly Label usage;

    public SettingsDialog(Settings settings, AppCache cache)
    {
        this.cache = cache;
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Theme.SolidSurfaceRaised;
        ForeColor = Theme.ColorText;
        Font = Theme.Body(Theme.TextSm);
        Padding = new Padding(Theme.Space6);

        Label Prose(string text, int top) => new()
        {
            Text = text, AutoSize = true, MaximumSize = new Size(LogicalToDeviceUnits(520), 0),
            Font = Theme.Sans(Theme.TextSm), ForeColor = Theme.ColorTextMuted, Margin = new Padding(0, top, 0, 0),
        };

        location = new HudTextBox(BackColor) { Dock = DockStyle.Fill, Margin = Padding.Empty };
        location.Text = string.IsNullOrWhiteSpace(settings.CacheLocation) ? AppCache.DefaultLocation : settings.CacheLocation;
        var browse = new HudButton("Browse", ButtonKind.Secondary) { Margin = new Padding(Theme.Space2, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            // The folder picker belongs to Windows; it is the one native surface we leave as is.
            using var fb = new FolderBrowserDialog { SelectedPath = location.Text, UseDescriptionForTitle = true, Description = "Keep the cache in" };
            if (fb.ShowDialog(this) == DialogResult.OK) location.Text = fb.SelectedPath;
        };
        var locationRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, Theme.Space1, 0, 0), BackColor = BackColor };
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        locationRow.Controls.Add(location, 0, 0);
        locationRow.Controls.Add(browse, 1, 0);

        limit = new HudTextBox(BackColor) { Width = LogicalToDeviceUnits(96), Margin = Padding.Empty };
        limit.Text = settings.CacheLimitGb.ToString();
        var gb = new Label { Text = "GB", AutoSize = true, Anchor = AnchorStyles.Left, Font = Theme.Body(Theme.TextSm), ForeColor = Theme.ColorTextMuted, Margin = new Padding(Theme.Space2, 0, 0, 0) };
        var limitRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = BackColor, Margin = new Padding(0, Theme.Space1, 0, 0) };
        limitRow.Controls.Add(limit);
        limitRow.Controls.Add(gb);

        usage = new Label { AutoSize = true, Font = Theme.Body(Theme.TextSm), ForeColor = Theme.ColorText, Margin = new Padding(0, Theme.Space4, 0, Theme.Space1) };
        var empty = new HudButton("Empty cache", ButtonKind.Secondary) { Margin = Padding.Empty };
        empty.Click += (_, _) => { cache.Clear(); ShowUsage(); };
        ShowUsage();

        var ok = new HudButton("Save", ButtonKind.Primary) { DialogResult = DialogResult.OK };
        var cancel = new HudButton("Cancel", ButtonKind.Ghost) { DialogResult = DialogResult.Cancel };
        AcceptButton = ok;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, Theme.Space5, 0, 0), BackColor = BackColor };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = BackColor,
            Location = new Point(LogicalToDeviceUnits(Theme.Space6), LogicalToDeviceUnits(Theme.Space5)),
            MinimumSize = new Size(LogicalToDeviceUnits(540), 0),
        };
        layout.Controls.Add(new HudLabel("Cache"));
        layout.Controls.Add(Prose("The cache keeps thumbnails, files you open, and copies made while checking for duplicates, so they're quick next time. " +
                                  $"It is kept in a \"{AppCache.FolderName}\" folder inside the location below. When it reaches its limit, the files used least recently are removed.", Theme.Space1));
        layout.Controls.Add(new HudLabel("Location") { Margin = new Padding(0, Theme.Space4, 0, 0) });
        layout.Controls.Add(locationRow);
        layout.Controls.Add(Prose("Changing it moves the cache there, most recently used files first. If the new drive fills up, or you cancel, the rest are dropped rather than left behind.", Theme.Space1));
        layout.Controls.Add(new HudLabel("Limit") { Margin = new Padding(0, Theme.Space4, 0, 0) });
        layout.Controls.Add(limitRow);
        layout.Controls.Add(usage);
        layout.Controls.Add(empty);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) return;
            var path = location.Text.Trim();
            if (!Path.IsPathFullyQualified(path))
            {
                HudDialog.Show(this, "Choose a folder", "Enter a full folder path for the cache, for example D:\\Cache, or use Browse.", ("Close", ButtonKind.Ghost, DialogResult.Cancel));
                e.Cancel = true;
                return;
            }
            if (!int.TryParse(limit.Text.Trim(), out var size) || size is < 1 or > 10_000)
            {
                HudDialog.Show(this, "Check the limit", "Enter a limit in whole gigabytes, from 1 to 10000.", ("Close", ButtonKind.Ghost, DialogResult.Cancel));
                e.Cancel = true;
                return;
            }
            if (!cache.IsAt(path) && !AppCache.CanUse(path))
            {
                HudDialog.Show(this, "Choose another folder",
                    $"{AppCache.RootFor(path)} already has other files in it, so the cache can't use it. Choose another location, or empty that folder first.",
                    ("Close", ButtonKind.Ghost, DialogResult.Cancel));
                e.Cancel = true;
                return;
            }
            // The caller moves the cache (it can take a while) and applies the limit.
            settings.CacheLocation = string.Equals(path.TrimEnd('\\'), AppCache.DefaultLocation, StringComparison.OrdinalIgnoreCase) ? "" : path;
            settings.CacheLimitGb = size;
            settings.Save();
        };
    }

    void ShowUsage() =>
        usage.Text = $"Using {ThumbGrid.FormatSize(cache.Used)} of {ThumbGrid.FormatSize(cache.Limit)} in {cache.Root}";

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HudWindow.Apply(this);
    }
}
