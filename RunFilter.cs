namespace StS2WinRateOverlay;

internal sealed record RunFilter(
    int? Ascension,
    string? Character,
    string? BuildId,
    DateTime StartTimeFrom,
    DateTime StartTimeTo,
    bool StandardOnly)
{
    public static RunFilter Default => new(
        10,
        "CHARACTER.IRONCLAD",
        null,
        DateTime.MinValue,
        DateTime.MaxValue,
        true);

    public string AscensionLabel => Ascension.HasValue ? $"A{Ascension.Value}" : "All A";

    public string CharacterLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Character))
            {
                return "All Characters";
            }

            return Character.Split('.').LastOrDefault() ?? Character;
        }
    }

    public string GameModeLabel => StandardOnly ? "Standard" : "All";
}
