using PhoneExplorer.Hud;

namespace PhoneExplorer;

/// <summary>Sets up a duplicate check: which PC folders to compare the phone with, and whether to compare exactly.
/// Writes the choices back to Settings on OK.</summary>
public sealed class DuplicatesDialog : Form
{
    readonly List<string> folders;
    readonly TableLayoutPanel list;
    readonly HudCheck exact = new("Exact match: compare the pictures pixel by pixel");
    readonly HudButton ok;

    public DuplicatesDialog(Settings settings, string device)
    {
        folders = (settings.DuplicateFolders ?? new List<string> { settings.Destination }).ToList();
        Text = "Find on PC";
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
        exact.Checked = settings.DuplicateExact;

        Label Prose(string text, int top) => new()
        {
            Text = text, AutoSize = true, MaximumSize = new Size(LogicalToDeviceUnits(540), 0),
            Font = Theme.Sans(Theme.TextSm), ForeColor = Theme.ColorTextMuted, Margin = new Padding(0, top, 0, 0),
        };

        list = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, BackColor = BackColor, Margin = new Padding(0, Theme.Space1, 0, Theme.Space2) };
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var add = new HudButton("Add folder", ButtonKind.Secondary) { Margin = Padding.Empty };
        add.Click += (_, _) =>
        {
            // The folder picker belongs to Windows; it is the one native surface we leave as is.
            using var fb = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = "Compare the phone with this folder", SelectedPath = folders.LastOrDefault() ?? "" };
            if (fb.ShowDialog(this) != DialogResult.OK) return;
            if (!folders.Contains(fb.SelectedPath, StringComparer.OrdinalIgnoreCase)) folders.Add(fb.SelectedPath);
            Fill();
        };

        ok = new HudButton("Find", ButtonKind.Primary) { DialogResult = DialogResult.OK };
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
            MinimumSize = new Size(LogicalToDeviceUnits(560), 0),
        };
        layout.Controls.Add(Prose($"Finds items on {device} that are already in these folders on this PC, including their subfolders. " +
                                  "You can then select them and delete them from the phone to free up space. Nothing is changed on this PC.", 0));
        layout.Controls.Add(new HudLabel("Folders on this PC") { Margin = new Padding(0, Theme.Space4, 0, Theme.Space1) });
        layout.Controls.Add(list);
        layout.Controls.Add(add);
        layout.Controls.Add(new HudLabel("How closely to compare") { Margin = new Padding(0, Theme.Space5, 0, Theme.Space1) });
        layout.Controls.Add(exact);
        layout.Controls.Add(Prose("Without it, items match on name, size, and the time they were taken. " +
                                  "With it, each matching photo is also copied from the phone and compared pixel by pixel, and videos also on length and resolution. " +
                                  "That's slower, especially for videos, which are copied in full.", Theme.Space1));
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        Fill();

        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) return;
            settings.DuplicateFolders = folders.ToList();
            settings.DuplicateExact = exact.Checked;
            settings.Save();
        };
    }

    public List<string> Folders => folders.ToList();
    public bool ExactMatch => exact.Checked;

    /// <summary>One row per folder, each with a Remove button; a note when the list is empty.</summary>
    void Fill()
    {
        list.SuspendLayout();
        list.Controls.Clear();
        list.RowStyles.Clear();
        for (int i = 0; i < folders.Count; i++)
        {
            var path = folders[i];
            var label = new Label
            {
                Text = path, AutoSize = true, Anchor = AnchorStyles.Left, AutoEllipsis = true, MaximumSize = new Size(LogicalToDeviceUnits(440), 0),
                Font = Theme.Body(Theme.TextSm), ForeColor = Directory.Exists(path) ? Theme.ColorText : Theme.ColorTextDim, Margin = new Padding(0, 0, Theme.Space2, 0),
            };
            if (!Directory.Exists(path)) label.Text += "  (not found)";
            var remove = new HudButton("Remove", ButtonKind.Ghost) { Margin = new Padding(0, Theme.Space1, 0, Theme.Space1) };
            remove.Click += (_, _) => { folders.Remove(path); Fill(); };
            list.Controls.Add(label, 0, i);
            list.Controls.Add(remove, 1, i);
        }
        if (folders.Count == 0)
            list.Controls.Add(new Label { Text = "No folders yet. Add the folders where you keep your photos.", AutoSize = true, Font = Theme.Sans(Theme.TextSm), ForeColor = Theme.ColorTextMuted }, 0, 0);
        list.ResumeLayout(true);
        ok.Enabled = folders.Any(Directory.Exists);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HudWindow.Apply(this);
    }
}
