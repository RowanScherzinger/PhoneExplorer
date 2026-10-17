# CLAUDE.md

Phone Explorer (formerly iPhone Explorer): a small C# WinForms (.NET 10) app for browsing and
copying photos off an iPhone or Android phone over USB. Source in `src/PhoneExplorer`. Build with
`dotnet publish src\PhoneExplorer -c Release -o publish`, then copy `publish\PhoneExplorer.exe` to the repo root.

Releases are self-contained builds (see README) attached to GitHub releases on
RowanScherzinger/PhoneExplorer. `Updater.cs` finds them by tag `vX.Y.Z` and the asset name
`PhoneExplorer.exe`, so keep both, and bump `<Version>` in the csproj before each release.

## Visual style
The app has swappable themes, one per style guide, picked in the status bar and saved as
`Settings.ThemeName`:
- Jarvis (default): `F:\Projects\StyleGuides\JarvisApps` (read its CLAUDE.md first).
- Pastel Girl: `F:\Projects\StyleGuides\PastelGirl` (dark variant).

This is a native app, so it can't use `tokens.css` directly. Each guide's tokens are mirrored
by hand in `src/PhoneExplorer/Hud/Themes/<Guide>.cs` (a `ThemeDef`). Code reads them only
through `Hud/Theme.cs`. Never hardcode a colour, font, size or duration outside `Hud/Theme.cs`,
`Hud/ThemeDef.cs` and `Hud/Themes/`. Before finishing a UI change, search for `SystemColors`,
`Color.FromArgb`, `MessageBox.Show` and `new Font(` outside those files. That search should find nothing.

Rules that keep themes swappable:
- Paint code reads `Theme` on every paint. A control that caches a theme value (Font, size,
  BackColor) needs an `ApplyTheme()` that `MainForm.ApplyTheme` calls.
- Write label, heading and button text in sentence case. `Theme.Case()` uppercases it for HUD themes.
- Use the role helpers: `Theme.Body/Label/Heading/ButtonFont/Number/BigNumber`,
  `Theme.Fill/Stroke` with a `Corner`, and `Theme.DrawStatus`. HUD-only details (brackets, edge
  bars, boot flicker) check `Theme.Hud`.
- Adding a guide means a new `Themes/*.cs` and an entry in `Theme.All`.

## Phone access
All shell/WPD access goes through `PhoneWorker`'s single STA thread. Never create shell items on
the UI thread, and never rebuild an item from a path string: WPD paths don't round-trip.
`PhoneDevice.Id` (the parsing name) is only for telling devices apart.

iPhone and Android differ (`PhoneDevice.IsApple`):
- iPhone: the whole first storage is walked (it holds only `YYYYMM_a` folders), and
  `Library.Build` groups edits, Live Photo clips and `.AAE` files with their original. Keep the
  iPhone's `MediaFile.Folder` strings unchanged: `SeenStore` keys on them, and a change marks
  every photo as new.
- Android: only `DCIM` and `Pictures` on each storage are walked. Storages after the first are
  prefixed with their name. Names starting with `.` are skipped. Every file is its own item.
