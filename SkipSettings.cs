using System;
using System.Globalization;
using System.IO;

namespace SkipTraining;

// Which horses a skip option applies to. Boarders are the livery horses clients keep at the yard.
internal enum HorseScope
{
    Off,
    OwnedHorses,
    Boarders,
    AllHorses
}

internal static class HorseScopeExtensions
{
    public static bool Includes(this HorseScope scope, bool boarder) => scope switch
    {
        HorseScope.AllHorses => true,
        HorseScope.OwnedHorses => !boarder,
        HorseScope.Boarders => boarder,
        _ => false
    };

    public static bool IncludesOwned(this HorseScope scope) => scope.Includes(false);
    public static bool IncludesBoarders(this HorseScope scope) => scope.Includes(true);
}

// The skip settings. They are the same for every save game and are changed in the Mod Manager's
// mod window; the file lives next to the plugin's other config.
internal class SkipSettings
{
    private static readonly string FilePath = Path.Combine(BepInEx.Paths.ConfigPath, "hoofprintbay.skiptraining.skip-settings.cfg");

    public HorseScope Training = HorseScope.AllHorses;
    public int TrainingOwnedConfidence = 50;
    public int TrainingBoarderConfidence = 50;
    public HorseScope TrailRides = HorseScope.AllHorses;
    public int TrailOwnedConfidence = 50;
    public int TrailBoarderConfidence = 50;
    public bool Lessons = true;
    public bool LessonTips;
    public int LessonConfidence = 50;
    public bool Competitions = true;
    public bool HorseInfoResults = true;

    // What a horse needs to give each lesson (see LessonRequirements); -1 = the game's own value.
    public readonly int[] LessonMinConfidence = { -1, -1, -1, -1 };
    public readonly int[] LessonMinStrength = { -1, -1, -1, -1 };
    public readonly int[] LessonMinEndurance = { -1, -1, -1, -1 };

    private static string LessonKey(int lesson, string stat) => $"Lesson{LessonRequirements.Levels[lesson]}{stat}";

    private static SkipSettings current;

    public static SkipSettings Current => current ??= Load();

    private static SkipSettings Load()
    {
        var settings = new SkipSettings();
        if (!File.Exists(FilePath))
            return settings;
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var separator = line.IndexOf('=');
                if (line.StartsWith("#") || separator <= 0)
                    continue;
                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                switch (key)
                {
                    case nameof(Training): settings.Training = ParseScope(value, settings.Training); break;
                    case nameof(TrainingOwnedConfidence): settings.TrainingOwnedConfidence = ParsePercent(value, settings.TrainingOwnedConfidence); break;
                    case nameof(TrainingBoarderConfidence): settings.TrainingBoarderConfidence = ParsePercent(value, settings.TrainingBoarderConfidence); break;
                    case nameof(TrailRides): settings.TrailRides = ParseScope(value, settings.TrailRides); break;
                    case nameof(TrailOwnedConfidence): settings.TrailOwnedConfidence = ParsePercent(value, settings.TrailOwnedConfidence); break;
                    case nameof(TrailBoarderConfidence): settings.TrailBoarderConfidence = ParsePercent(value, settings.TrailBoarderConfidence); break;
                    case nameof(Lessons): settings.Lessons = ParseBool(value, settings.Lessons); break;
                    case nameof(LessonTips): settings.LessonTips = ParseBool(value, settings.LessonTips); break;
                    case nameof(LessonConfidence): settings.LessonConfidence = ParsePercent(value, settings.LessonConfidence); break;
                    case nameof(Competitions): settings.Competitions = ParseBool(value, settings.Competitions); break;
                    case nameof(HorseInfoResults): settings.HorseInfoResults = ParseBool(value, settings.HorseInfoResults); break;
                    default:
                        for (var i = 0; i < LessonRequirements.Levels.Length; i++)
                        {
                            if (key == LessonKey(i, "Confidence")) settings.LessonMinConfidence[i] = ParseOverride(value);
                            else if (key == LessonKey(i, "Strength")) settings.LessonMinStrength[i] = ParseOverride(value);
                            else if (key == LessonKey(i, "Endurance")) settings.LessonMinEndurance[i] = ParseOverride(value);
                        }
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Reading {FilePath} failed, using defaults: {e.Message}");
        }
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var lines = new System.Collections.Generic.List<string>
            {
                "# Skip Training mod settings, the same for all save games. Change them in the Mods menu of the game.",
                $"{nameof(Training)}={Training}",
                $"{nameof(TrainingOwnedConfidence)}={TrainingOwnedConfidence}",
                $"{nameof(TrainingBoarderConfidence)}={TrainingBoarderConfidence}",
                $"{nameof(TrailRides)}={TrailRides}",
                $"{nameof(TrailOwnedConfidence)}={TrailOwnedConfidence}",
                $"{nameof(TrailBoarderConfidence)}={TrailBoarderConfidence}",
                $"{nameof(Lessons)}={Lessons}",
                $"{nameof(LessonTips)}={LessonTips}",
                $"{nameof(LessonConfidence)}={LessonConfidence}",
                $"{nameof(Competitions)}={Competitions}",
                $"{nameof(HorseInfoResults)}={HorseInfoResults}",
                "# Lesson horse needs (-1 = the game's own value).",
            };
            for (var i = 0; i < LessonRequirements.Levels.Length; i++)
            {
                lines.Add($"{LessonKey(i, "Confidence")}={LessonMinConfidence[i]}");
                lines.Add($"{LessonKey(i, "Strength")}={LessonMinStrength[i]}");
                lines.Add($"{LessonKey(i, "Endurance")}={LessonMinEndurance[i]}");
            }
            File.WriteAllLines(FilePath, lines);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Writing {FilePath} failed: {e.Message}");
        }
    }

    private static HorseScope ParseScope(string value, HorseScope fallback) =>
        Enum.TryParse(value, true, out HorseScope scope) && Enum.IsDefined(typeof(HorseScope), scope) ? scope : fallback;

    private static int ParsePercent(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) ? Math.Clamp(percent, 0, 100) : fallback;

    private static int ParseOverride(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0 ? Math.Min(v, 100) : -1;

    private static bool ParseBool(string value, bool fallback) =>
        bool.TryParse(value, out var flag) ? flag : fallback;
}
