# CLAUDE.md

Phone Explorer (formerly iPhone Explorer): a small C# WinForms (.NET 10) app for browsing and
copying photos off an iPhone or Android phone over USB. Source in `src/PhoneExplorer`. Build with
`dotnet publish src\PhoneExplorer -c Release -o publish`, then copy `publish\PhoneExplorer.exe` to the repo root.

Tests: `dotnet test tests\PhoneExplorer.Tests`. Run them before every build you hand over, and keep them
passing. Any change to copying, moving, deleting or `FileCheck` needs a test that fails without it
(`CopyEngineTests` drives `CopyEngine` with `FakePhone`, which can go out of step like the iPhone did).

Releases are self-contained builds (see README) attached to GitHub releases on
RowanScherzinger/PhoneExplorer. `Updater.cs` finds them by tag `vX.Y.Z` and the asset name
`PhoneExplorer.exe`, so keep both, and bump `<Version>` in the csproj before each release.

## Visual style
The app has swappable themes, picked in the status bar and saved as `Settings.ThemeName`:
- Windows (default): `F:\Projects\StyleGuides\Win11` (read its CLAUDE.md first), the Windows 11
  Fluent look mirrored from WinUI. It follows the system light/dark mode and accent colour, live
  (`Theme.Refresh` on `UserPreferenceChanged`), so its `ThemeDef` is rebuilt: compare themes by `Id`.
- Jarvis: `F:\Projects\StyleGuides\JarvisApps` (read its CLAUDE.md first).
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
  bars, boot flicker) check `Theme.Hud`. Cute details (hearts, sparkles, `Theme.Say` copy) check
  `Theme.Cute`; a soft theme without them (Windows) is plain.
- Native parts (scrollbars, text boxes, common dialogs) follow `ThemeDef.Dark` through
  `Theme.ApplyColorMode`, set before the first window; `HudWindow.Native` re-skins existing ones.
- Adding a guide means a new `Themes/*.cs` and an entry in `Theme.All`.

## Disk cache
Everything the app keeps on disk besides settings goes through `AppCache`: thumbnails, opened files,
files pulled for duplicate checks, and the PC folder index. Never write temp or cache files anywhere
else. Write a file with `Put` (or `Stage` then `Commit`), and read it with `Get`.
- The cache owns only its `PhoneExplorer cache` folder, and only when its marker file is present.
- At startup it deletes anything there that `index.json` doesn't list.
- It evicts least-recently-used entries to stay under `Settings.CacheLimitGb`.
- Key phone files with `PhoneWorker.FileKey` (device, folder, name, size). Ids change on every scan.

## Duplicates
`DuplicateFinder` compares phone files with PC folders.
- The phone gives only name, size and date over USB: no resolution or duration. Anything more needs the
  file pulled into the cache through `PhoneWorker.Fetch`. Pull only files that already matched.
- Read dates the same way on both sides (`Shell.Date`/`LocalProperties`, FILETIME → local). PowerShell's `LastWriteTime` can be an hour off.
- The folders checked never include the cache, so a cached copy can't count as a backup.
- Delete removes the phone copy. "Select all duplicates" takes only `Exact` and `SameFile` items, where every file except `.AAE` matched.

## Phone access
All shell/WPD access goes through `PhoneWorker`'s single STA thread. Never create shell items on
the UI thread, and never rebuild an item from a path string: WPD paths don't round-trip.
`PhoneDevice.Id` (the parsing name) is only for telling devices apart.

Never trust a copy from the phone by its size alone. After one failed transfer an iPhone can stay out of
step, sending each later file under the right name but with the previous file's data, cut to the
requested size (this destroyed ~160 photos on 2026-11-08). So:
- A file counts as copied (and may be deleted from the phone) only if `FileCheck.Problem` passes too,
  and the same goes for a file already on the PC and for a match in Find on PC.
- The copy rules live in `CopyEngine` (not `PhoneWorker`, so they can be tested). It stops at the first
  failure, and `CopyEngine.Trouble` refuses copies and fetches until the next scan.
- A failed copy's file is deleted, so a broken file never sits under a photo's name.
- A Move deletes a phone file only when `CopyResult.SafeToDelete` says so: its PC copy (new or already
  there) was read back from the phone and matched byte for byte, and the probes (small files already
  confirmed, read in turn) still read back right afterwards. A copy and its read-back can both be wrong
  in the same way; a probe that wasn't asked for recently can't. Never delete on `Verified` (size and structure) alone.
- Delete counts an item as "already on this PC" only after `CopyEngine.Confirm` reads its PC copies
  (Find on PC matches, copy folder) back from the phone the same way. When only some are confirmed, the
  dialog offers to delete just those.

Companion files (edits, Live Photo clips, `.AAE`) are handled behind the scenes: messages and
progress name the item (`MediaEntry.Describe`/`PartName`), never a companion file, and counts are items.

iPhone and Android differ (`PhoneDevice.IsApple`):
- iPhone: the whole first storage is walked (it holds only `YYYYMM_a` folders), and
  `Library.Build` groups edits, Live Photo clips and `.AAE` files with their original. Keep the
  iPhone's `MediaFile.Folder` strings unchanged: `SeenStore` keys on them, and a change marks
  every photo as new.
- Android: only `DCIM` and `Pictures` on each storage are walked. Storages after the first are
  prefixed with their name. Names starting with `.` are skipped. Every file is its own item.
