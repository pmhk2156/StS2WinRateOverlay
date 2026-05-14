using System.Text.Json;

namespace StS2WinRateOverlay;

internal static class RunHistoryReader
{
    public static RunStats Read(string historyDirectory, RunFilter filter)
    {
        if (!Directory.Exists(historyDirectory))
        {
            return RunStats.MissingDirectory(historyDirectory);
        }

        var total = 0;
        var wins = 0;
        var errors = 0;
        var matchedRuns = new List<RunResult>();

        foreach (var file in Directory.EnumerateFiles(historyDirectory, "*.run"))
        {
            try
            {
                using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                if (filter.Ascension.HasValue
                    && (!TryGetInt(root, "ascension", out var runAscension) || runAscension != filter.Ascension.Value))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(filter.Character) && !HasCharacter(root, filter.Character))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(filter.BuildId)
                    && (!TryGetString(root, "build_id", out var buildId)
                        || !string.Equals(buildId, filter.BuildId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (filter.StandardOnly
                    && (!TryGetString(root, "game_mode", out var gameMode)
                        || !string.Equals(gameMode, "standard", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!TryGetInt64(root, "start_time", out var startTime))
                {
                    continue;
                }

                var startedAt = DateTimeOffset.FromUnixTimeSeconds(startTime).LocalDateTime;
                if (startedAt < filter.StartTimeFrom || startedAt > filter.StartTimeTo)
                {
                    continue;
                }

                if (!TryGetBool(root, "win", out var didWin))
                {
                    didWin = false;
                }

                total++;
                matchedRuns.Add(new RunResult(startTime, didWin));
                if (didWin)
                {
                    wins++;
                }
            }
            catch (IOException)
            {
                errors++;
            }
            catch (UnauthorizedAccessException)
            {
                errors++;
            }
            catch (JsonException)
            {
                errors++;
            }
        }

        return new RunStats(total, wins, CalculateCurrentWinStreak(matchedRuns), errors, historyDirectory);
    }

    private static int CalculateCurrentWinStreak(List<RunResult> runs)
    {
        var streak = 0;
        foreach (var run in runs.OrderByDescending(run => run.StartTime))
        {
            if (!run.DidWin)
            {
                break;
            }

            streak++;
        }

        return streak;
    }

    private static bool HasCharacter(JsonElement root, string character)
    {
        if (!root.TryGetProperty("players", out var players))
        {
            return false;
        }

        if (players.ValueKind == JsonValueKind.Array)
        {
            foreach (var player in players.EnumerateArray())
            {
                if (HasCharacterValue(player, character))
                {
                    return true;
                }
            }

            return false;
        }

        return players.ValueKind == JsonValueKind.Object && HasCharacterValue(players, character);
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool HasCharacterValue(JsonElement player, string character)
    {
        return player.TryGetProperty("character", out var value)
            && value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), character, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetInt(JsonElement root, string propertyName, out int value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryGetInt64(JsonElement root, string propertyName, out long value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value);
    }

    private static bool TryGetBool(JsonElement root, string propertyName, out bool value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var property)
            || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private sealed record RunResult(long StartTime, bool DidWin);
}
