
using System.Drawing;
using Microsoft.Win32;

namespace PhoneExplorer.Hud.Themes;

public static class WindowsTheme
{
    const string RegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDark
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
                return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static ThemeDef Create()
    {
        bool dark = IsDark;

        Color C(string light, string darkValue) =>
            ThemeDef.Hex(dark ? darkValue : light);

        return new ThemeDef
        {
            Id = "Windows",
            DisplayName = "Windows 11",
            Dark = dark,

            Bg = C("#F3F3F3", "#202020"),
            BgElevated = C("#FFFFFF", "#282828"),
            Surface = C("#FFFFFF", "#2B2B2B"),
            SurfaceHover = C("#F5F5F5", "#353535"),
            SurfaceRaised = C("#FFFFFF", "#323232"),

            Border = C("#E5E5E5", "#414141"),
            BorderStrong = C("#D1D1D1", "#555555"),
            Divider = C("#E5E5E5", "#3B3B3B"),

            Text = C("#333333", "#F3F3F3"),
            TextStrong = C("#1A1A1A", "#FFFFFF"),
            TextMuted = C("#666666", "#BDBDBD"),
            TextDim = C("#888888", "#929292"),

            Accent = C("#0078D4", "#60CDFF"),
            AccentText = C("#0067B8", "#60CDFF"),
            AccentHover = C("#0067B8", "#75D4FF"),
            AccentSoft = C("#DCEEFF", "#183B50"),
            OnAccent = C("#FFFFFF", "#003044"),

            Highlight = C("#0078D4", "#60CDFF"),
            HighlightSoft = C("#E5F2FC", "#183B50"),

            Select = C("#DCEEFF", "#183B50"),
            SelectSoft = C("#EAF4FC", "#20445A"),
            SelectRing = C("#0078D4", "#60CDFF"),

            Progress = C("#0078D4", "#60CDFF"),
            Eyebrow = C("#666666", "#BDBDBD"),

            StatusOk = C("#107C10", "#6CCB5F"),
            StatusPending = C("#0078D4", "#60CDFF"),
            StatusIdle = C("#888888", "#929292"),
            StatusWarning = C("#C19C00", "#FCE100"),
            StatusDanger = C("#D13438", "#FF6961"),

            DangerText = C("#A4262C", "#FF9999"),
            DangerFill = C("#FDE7E9", "#442326"),
            DangerBorder = C("#E5A0A3", "#71383B"),

            Info = C("#0078D4", "#60CDFF"),
            FocusRing = C("#0078D4", "#60CDFF"),

            InputFill = C("#FFFFFF", "#292929"),
            Scrim = Color.FromArgb(100, 0, 0, 0),
            NeutralData = C("#737373", "#A0A0A0"),

            Body = new Face("Segoe UI Variable", FontStyle.Regular),
            Sans = new Face("Segoe UI Variable", FontStyle.Regular),
            Mono = new Face("Cascadia Code", FontStyle.Regular),
            Label = new Face("Segoe UI Variable", FontStyle.Regular),
            Heading = new Face("Segoe UI Variable", FontStyle.SemiBold),
            Button = new Face("Segoe UI Variable", FontStyle.SemiBold),
            Number = new Face("Segoe UI Variable", FontStyle.Regular),
            BigNumber = new Face("Segoe UI Variable", FontStyle.SemiBold),

            Text2xs = 10f,
            TextXs = 11f,
            TextSm = 12f,
            TextMd = 14f,
            TextLg = 16f,
            TextXl = 20f,
            Text2xl = 24f,
            Text3xl = 30f,
            TextTitle = 28f,

            TrackingLabel = 0f,
            TrackingWide = 0f,
            Uppercase = false,

            RadiusSm = 4,
            RadiusMd = 6,
            RadiusLg = 8,
            RadiusXl = 12,
            Pills = false,

            ControlHeight = 32,
            PrimaryHeight = 36,

            DurationFast = 120,
            DurationNormal = 200,
            LoopPulse = 0,

            Hud = false
        };
    }
}
