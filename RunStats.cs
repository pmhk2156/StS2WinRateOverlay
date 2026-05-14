namespace StS2WinRateOverlay;

internal sealed record RunStats(int Total, int Wins, int CurrentWinStreak, int Errors, string HistoryDirectory)
{
    public double WinRate => Total == 0 ? 0 : (double)Wins / Total;

    public static RunStats MissingDirectory(string historyDirectory)
    {
        return new RunStats(0, 0, 0, -1, historyDirectory);
    }
}
