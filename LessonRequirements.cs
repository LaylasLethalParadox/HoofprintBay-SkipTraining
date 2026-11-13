using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SkipTraining;

// The confidence, strength and endurance a horse needs to give each riding lesson. The game keeps
// them on the lessons in CourseManager.ridingLessons and its lesson horse list reads them from there,
// so the settings are written into those lessons; a lesson without a setting gets the game's value back.
[HarmonyPatch]
internal static class LessonRequirements
{
    public static readonly string[] Levels = { "Beginner", "Novice", "Intermediate", "Advanced" };
    private static readonly string[] Ids = { "lesson.beginner", "lesson.novice", "lesson.intermediate", "lesson.advanced" };

    // The game's values (read 2026-11-13), shown before a save is loaded; replaced by the real ones when seen.
    private static readonly int[] GameConfidence = { 80, 70, 60, 50 };
    private static readonly int[] GameStrength = { 15, 20, 40, 70 };
    private static readonly int[] GameEndurance = { 10, 12, 30, 50 };

    private static readonly HashSet<int> seen = new();

    public static int GameValue(int level, Stat stat) => stat switch
    {
        Stat.Confidence => GameConfidence[level],
        Stat.Strength => GameStrength[level],
        _ => GameEndurance[level]
    };

    public enum Stat { Confidence, Strength, Endurance }

    [HarmonyPatch(typeof(CourseManager), nameof(CourseManager.Awake))]
    [HarmonyPostfix]
    private static void AfterAwake(CourseManager __instance) => Apply(__instance);

    // Called after a setting changes, so a loaded game uses it at once.
    public static void ApplyNow() => Apply(CourseManager.i);

    private static void Apply(CourseManager manager)
    {
        try
        {
            var lessons = manager != null ? manager.ridingLessons : null;
            if (lessons == null)
                return;
            var s = SkipSettings.Current;
            foreach (var lesson in lessons)
            {
                var level = lesson != null ? Array.IndexOf(Ids, lesson.id) : -1;
                if (level < 0)
                    continue;
                // The first lessons seen are the game's own; later ones may already hold our values.
                if (seen.Add(level))
                {
                    GameConfidence[level] = lesson.minimumConfidence;
                    GameStrength[level] = lesson.minimumStrength;
                    GameEndurance[level] = lesson.minimumEndurance;
                }
                lesson.minimumConfidence = Pick(s.LessonMinConfidence[level], GameConfidence[level]);
                lesson.minimumStrength = Pick(s.LessonMinStrength[level], GameStrength[level]);
                lesson.minimumEndurance = Pick(s.LessonMinEndurance[level], GameEndurance[level]);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Setting the lesson horse requirements failed: {e}");
        }
    }

    private static int Pick(int setting, int game) => setting >= 0 ? setting : game;
}
