namespace PhoneExplorer;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Native scrollbars and common controls follow the theme's light or dark mode: MainForm sets it
        // (Theme.ApplyColorMode) while loading settings, before any window is created.
        Application.Run(new MainForm());
    }
}
