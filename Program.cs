namespace StS2WinRateOverlay;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "StS2WinRateOverlay.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new OverlayForm(OverlayOptions.FromCommandLine(Environment.GetCommandLineArgs())));
    }    
}
