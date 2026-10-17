using PhoneExplorer.Hud;

namespace PhoneExplorer;

/// <summary>Asks where to copy to and which companion files to include. Writes choices back to Settings on OK.</summary>
public sealed class CopyDialog : Form
{
    readonly HudTextBox dest;
    readonly HudCheck organise;
    readonly HudCheck edits = new("Edited versions (IMG_E…) as well as the originals");
    readonly HudCheck live = new("Live Photo motion clips (the .MOV next to each Live Photo)");
    readonly HudCheck sidecars = new("Edit data files (.AAE)");
    readonly int count;
    readonly string size;

    /// <summary>Height of the painted readout above the controls, in logical px (at least 96; more for bigger theme fonts).</summary>
    static int ReadoutHeight => Math.Max(96, Theme.Space5 + Theme.Space4 + Theme.Space3 + Theme.Space4 +
        TextRenderer.MeasureText("0", Theme.BigNumber(Theme.Text2xl), Size.Empty, TextFormatFlags.NoPadding).Height);

    /// <param name="apple">iPhone: offer the companion files (edits, Live Photo clips, edit data). Other phones have none.</param>
    /// <param name="move">Move mode (cut and paste): every file of each item goes, so the companion
    /// choices are hidden, and the confirm button is a Danger button that Enter doesn't trigger.</param>
    public CopyDialog(Settings settings, int count, long bytes, string device, bool apple, bool move = false)
    {
        this.count = count;
        size = ThumbGrid.FormatSize(bytes);
        Text = move ? "Move to PC" : "Copy to PC";
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

        dest = new HudTextBox(BackColor) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 0) };
        var example = DateTime.Now;
        organise = new HudCheck($"Sort into year and month folders (for example {example:yyyy}\\{example:MM} {example:MMMM})");
        dest.Text = settings.Destination;
        organise.Checked = settings.Organise;
        edits.Checked = settings.IncludeEdits;
        live.Checked = settings.IncludeLive;
        sidecars.Checked = settings.IncludeSidecars;

        var browse = new HudButton("Browse", ButtonKind.Secondary) { Margin = new Padding(Theme.Space2, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            // The folder picker belongs to Windows; it is the one native surface we leave as is.
            using var fb = new FolderBrowserDialog { SelectedPath = dest.Text, UseDescriptionForTitle = true, Description = "Copy photos to" };
            if (fb.ShowDialog(this) == DialogResult.OK) dest.Text = fb.SelectedPath;
        };
        var ok = move
            ? new HudButton(count == 1 ? "Move 1 item" : $"Move {count:N0} items", ButtonKind.Danger) { DialogResult = DialogResult.OK }
            : new HudButton("Copy", ButtonKind.Primary) { DialogResult = DialogResult.OK };
        var cancel = new HudButton("Cancel", ButtonKind.Ghost) { DialogResult = DialogResult.Cancel };
        if (!move) AcceptButton = ok;
        CancelButton = cancel;

        var destRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, Theme.Space1, 0, Theme.Space3), BackColor = BackColor };
        destRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        destRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        destRow.Controls.Add(dest, 0, 0);
        destRow.Controls.Add(browse, 1, 0);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, Theme.Space5, 0, 0), BackColor = BackColor };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        HudLabel Caption(string text, int top = 0) => new(text) { Margin = new Padding(0, top, 0, Theme.Space1) };

        // Undocked, so place it below the painted readout by hand (form Padding only moves docked controls).
        var layout = new TableLayoutPanel
        {
            ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = BackColor,
            Location = new Point(LogicalToDeviceUnits(Theme.Space6), LogicalToDeviceUnits(ReadoutHeight)),
        };
        layout.Controls.Add(Caption("Destination"));
        layout.Controls.Add(destRow);
        layout.Controls.Add(organise);
        if (!move && apple)
        {
            layout.Controls.Add(Caption("Also copy", Theme.Space3));
            layout.Controls.Add(edits);
            layout.Controls.Add(live);
            layout.Controls.Add(sidecars);
        }
        layout.Controls.Add(new Label
        {
            Text = move && apple
                ? "Each item moves with its edited versions, Live Photo clips and edit data, so nothing is left behind. " +
                  "A file is deleted from the phone only after its copy on this PC is checked; if anything fails, that item stays on the phone. " +
                  "If iCloud Photos is on, moved items are also removed from iCloud and your other devices."
                : move
                ? "A file is deleted from the phone only after its copy on this PC is checked; if anything fails, that item stays on the phone. " +
                  "Deleted files don't go to the phone's Recycle bin. Copies already backed up to Google Photos or another cloud service stay there."
                : "Files already in the destination with the same name and size are skipped, so copying again is safe. Nothing is changed on the phone.",
            AutoSize = true, MaximumSize = new Size(LogicalToDeviceUnits(520), 0),
            Font = Theme.Sans(Theme.TextSm), ForeColor = Theme.ColorTextMuted, Margin = new Padding(0, Theme.Space3, 0, 0),
        });
        layout.Controls.Add(buttons);
        layout.MinimumSize = new Size(LogicalToDeviceUnits(540), 0);
        Controls.Add(layout);
        Tag = device;

        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) return;
            if (string.IsNullOrWhiteSpace(dest.Text) || !Path.IsPathFullyQualified(dest.Text.Trim()))
            {
                HudDialog.Show(this, "Choose a folder", "Enter a full folder path, for example D:\\Photos\\Phone, or use Browse.",
                    ("Close", ButtonKind.Ghost, DialogResult.Cancel));
                e.Cancel = true;
                return;
            }
            settings.Destination = dest.Text.Trim();
            settings.Organise = organise.Checked;
            if (move) { settings.Save(); return; }
            settings.IncludeEdits = edits.Checked;
            settings.IncludeLive = live.Checked;
            settings.IncludeSidecars = sidecars.Checked;
            settings.Save();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HudWindow.Apply(this);
    }

    /// <summary>The panel's lead readout: what is about to be copied.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        int x = LogicalToDeviceUnits(Theme.Space6), y = LogicalToDeviceUnits(Theme.Space5);
        Theme.DrawTracked(g, Theme.Case($"From {Tag}"), Theme.Label(), Theme.ColorEyebrow, new Point(x, y), Theme.TrackingLabel);
        y += LogicalToDeviceUnits(Theme.Space4);
        var vf = Theme.BigNumber(Theme.Text2xl);
        var value = count.ToString("N0");
        var vs = TextRenderer.MeasureText(g, value, vf, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, value, vf, new Point(x, y), Theme.ColorHighlight, TextFormatFlags.NoPadding);
        var unit = Theme.Case(count == 1 ? "item" : "items");
        var lf = Theme.Label(Theme.TextXs);
        var us = Theme.MeasureTracked(g, unit, lf, Theme.TrackingLabel);
        int ux = x + vs.Width + LogicalToDeviceUnits(Theme.Space1), uy = y + vs.Height - us.Height - LogicalToDeviceUnits(3);
        ux += Theme.DrawTracked(g, unit, lf, Theme.ColorTextMuted, new Point(ux, uy), Theme.TrackingLabel) + LogicalToDeviceUnits(Theme.Space4);
        TextRenderer.DrawText(g, size, Theme.Number(Theme.TextMd), new Point(ux, uy - LogicalToDeviceUnits(3)), Theme.ColorText, TextFormatFlags.NoPadding);
        using var pen = new Pen(Theme.Over(Theme.ColorBorder, BackColor));
        int ly = y + vs.Height + LogicalToDeviceUnits(Theme.Space3);
        g.DrawLine(pen, x, ly, ClientSize.Width - x, ly);
    }
}
