# Phone Explorer

A small Windows app for browsing and copying photos off an iPhone or Android phone over USB,
without digging through the phone's folders.

## Getting started

1. Go to the [latest release](https://github.com/RowanScherzinger/PhoneExplorer/releases/latest) and download `PhoneExplorer.exe`. There is nothing to install: it's one file that runs on Windows 10 and 11.
2. Double-click it. Windows may show "Windows protected your PC" because the app is new and unsigned. Click **More info**, then **Run anyway**.
3. Plug your phone into the PC with a USB cable and unlock it.
   - **Android:** pull down from the top of the screen, tap the USB notification, and choose **File transfer**.
   - **iPhone:** tap **Trust** when the phone asks.
4. Your photos appear in the app after a few seconds. Select some and click **COPY SELECTED** to save them on the PC.

The app checks for a new version each time it starts. When there is one, it asks, then downloads it, closes and opens again by itself.

## What it does

- All photos and videos in one grid, newest first, under month headings. The tree on the left jumps to a year or month.
- Filter by Photos / Videos / Screenshots & images, or by file name (Ctrl+F).
- **New since last time** lists items that appeared on the phone since the app last saw it, and marks them with a NEW badge. Right-click → "Mark everything as seen" clears the list.
- Select with click, Ctrl/Shift+click, Ctrl+A, or click a month heading to select the whole month.
- **COPY SELECTED** copies into `Year\MM Month` folders (optional) and skips files already copied. Ctrl+C copies the selection. Double-click opens a photo.
- **MOVE TO PC** (Ctrl+X) is cut and paste: it copies each item with all its files, checks every copy on the PC, and only then deletes it from the phone. If any file of an item fails, that item stays on the phone.
- **DELETE** (Del) removes items from the phone after a confirmation that shows how many are already in your copy folder. It can't be undone from the PC.
- Picks the phone up automatically when it is plugged in; F5 refreshes. With more than one phone plugged in, click the DEVICE panel to switch between them. All phones copy to the same folder.

## iPhone

- Unlock the phone and tap **Trust** when you plug it in.
- Edited versions (`IMG_E…`), Live Photo clips and `.AAE` edit files are shown as part of their original, not as separate items.
- Hidden photos are never shared over USB. To copy them, unhide them on the phone, press F5, copy them, then hide them again. They then show under "New since last time".
- With iCloud Photos on, deleting also deletes from iCloud and your other devices.
- Known limitation: thumbnails of some photos appear sideways because the phone does not report orientation to Windows. The copied files are not affected.

## Android

- Unlock the phone, tap the USB notification and choose **File transfer**. The phone shows up in This PC under its own name, for example `Rebecca Jay's S22`.
- The app reads `DCIM` (camera, Samsung screenshots) and `Pictures` (Pixel screenshots, saved images) on internal storage and on any SD card. Hidden folders such as `DCIM\.thumbnails` and items in the phone's trash are skipped.
- Every file is its own item.
- Treat deleting from the PC as permanent: it is not expected to go to the phone's Recycle bin. Copies already backed up to Google Photos stay there.

Settings are stored in `%APPDATA%\PhoneExplorer\settings.json`. The first run moves them over from `%APPDATA%\iPhoneExplorer`.

## Build

```
dotnet publish src\PhoneExplorer -c Release -o publish
```

Then copy `publish\PhoneExplorer.exe` to this folder. That build is small but needs the .NET 10 Desktop Runtime.

Release builds include the runtime, so they run on any Windows PC. To release, bump `<Version>` in the csproj, build as below, and attach the exe to a GitHub release tagged `vX.Y.Z` with the asset named exactly `PhoneExplorer.exe`. Running copies update to the newest non-draft, non-prerelease release.

```
dotnet publish src\PhoneExplorer -c Release -o publish -p:SelfContained=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Licence

MIT. See [LICENSE](LICENSE).
