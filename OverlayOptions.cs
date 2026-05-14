namespace StS2WinRateOverlay;

internal sealed record OverlayOptions(
    string HistoryDirectory,
    RunFilter InitialFilter,
    int? OverlayX,
    int? OverlayY)
{
    public static OverlayOptions FromCommandLine(string[] args)
    {
        var settings = AppSettingsStore.Load();
        var historyDirectory = settings.HistoryDirectory;
        var filter = settings.Filter;
        var ascension = filter.Ascension;
        var character = filter.Character;

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--history", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                historyDirectory = args[++i];
            }
            else if (arg.Equals("--ascension", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[++i], out var parsedAscension))
            {
                ascension = parsedAscension;
            }
            else if (arg.Equals("--character", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                character = args[++i];
            }
        }

        return new OverlayOptions(historyDirectory, filter with
        {
            Ascension = ascension,
            Character = character
        }, settings.OverlayX, settings.OverlayY);
    }
}
