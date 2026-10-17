namespace PhoneExplorer;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Dark only (style guide decisions/0002): native scrollbars, title bar and common controls render dark.
#pragma warning disable WFO5001
        Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
        Application.Run(new MainForm());
    }
}
