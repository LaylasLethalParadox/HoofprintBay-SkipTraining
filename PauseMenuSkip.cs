using System;
using HarmonyLib;

namespace SkipTraining;

// Adds a third choice to a pause dialog ("Continue" / "Abort ..." / "Skip ..."). The ride and lesson
// pause handlers fill only the first two choices of NotificationManager.ShowChoiceDialog, so the third slot is free.
// The hooks run inside the game's own pause menu code, so none of them may throw.
[HarmonyPatch]
internal static class PauseMenuSkip
{
    // Skip offered for the pause dialog that is being built right now.
    private static string offeredLabel;
    private static Action offeredSkip;

    // Pause dialog that carries the skip choice, and what the choice does.
    private static Notification skipDialog;
    private static Action skipAction;

    // Call before the game builds its pause dialog, and EndOffer once it is built.
    public static void Offer(string label, Action skip)
    {
        offeredLabel = label;
        offeredSkip = skip;
        skipDialog = null;
        skipAction = null;
    }

    public static void EndOffer()
    {
        offeredLabel = null;
        offeredSkip = null;
    }

    [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.ShowChoiceDialog))]
    [HarmonyPrefix]
    private static void AddSkipChoice(ref string choice3, ref VoidDelegateVoid action3, out bool __state)
    {
        __state = false;
        if (offeredSkip == null || !string.IsNullOrEmpty(choice3))
            return;
        try
        {
            action3 = new Action(OnSkipChosen);
            choice3 = offeredLabel;
            __state = true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Adding the skip choice failed: {e}");
        }
    }

    [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.ShowChoiceDialog))]
    [HarmonyPostfix]
    private static void KeepSkipLabel(Notification __result, bool __state)
    {
        if (!__state || __result == null)
            return;
        skipDialog = __result;
        skipAction = offeredSkip;
        try
        {
            // Choice labels are translation keys that the label's LanguageUpdater looks up in Start().
            // Ours is plain text and would come out as "Missing: ...", so keep it from being looked up.
            foreach (var updater in __result.buttonThird.GetComponentsInChildren<LanguageUpdater>(true))
                updater.enabled = false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Fixing the skip choice label failed: {e}");
        }
    }

    private static void OnSkipChosen()
    {
        var skip = skipAction;
        var dialog = skipDialog;
        skipAction = null;
        skipDialog = null;
        if (skip == null)
            return;

        try
        {
            skip();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Skipping failed: {e}");
        }

        // The dialog's buttons close it on their own (like "Abort"), which also unpauses the game.
        // Closing it again is harmless and covers a third button that lacks that wiring.
        try
        {
            if (dialog != null)
                dialog.CloseNotification();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Closing the pause dialog failed: {e}");
        }
    }
}
