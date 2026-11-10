using System;
using HarmonyLib;

namespace SkipTraining;

// Keeps the competition records in step with the game's save: written when the game saves,
// dropped when a save is loaded or deleted (so a crash or quitting without saving undoes both).
[HarmonyPatch]
internal static class GameSaveSync
{
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.Save))]
    [HarmonyPostfix]
    private static void AfterSave()
    {
        try
        {
            CompetitionRecords.SaveWithGame();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Saving the competition records failed: {e}");
        }
    }

    [HarmonyPatch(typeof(DataManager), nameof(DataManager.LoadFromSaveFromFile))]
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.LoadSaveFromJsonOrBinary))]
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.LoadSaveFromProtectedFile))]
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.DeleteSaveFile))]
    [HarmonyPrefix]
    private static void BeforeLoadOrDelete() => CompetitionRecords.Forget();
}
