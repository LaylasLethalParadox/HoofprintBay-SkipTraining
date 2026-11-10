using System;
using HarmonyLib;

namespace SkipTraining;

// Skipping riding lessons from the lesson's pause menu. A skipped lesson counts as a "good" lesson:
// the client pays the lesson fee, and the good-lesson tip only when the save's settings allow it.
[HarmonyPatch]
internal static class LessonSkip
{
    [HarmonyPatch(typeof(RidingLesson), nameof(RidingLesson.PauseLesson))]
    [HarmonyPrefix]
    private static void BeforePauseLesson(RidingLesson __instance)
    {
        PauseMenuSkip.EndOffer();
        try
        {
            if (!CanSkip(__instance))
                return;
            var lesson = __instance;
            PauseMenuSkip.Offer(Plugin.LessonButtonLabel.Value, () =>
            {
                if (!lesson.running)
                    return;
                var tips = SkipSettings.Current.LessonTips;
                FinishAsGoodLesson(lesson, tips);
                Plugin.Logger.LogInfo(tips ? "Lesson skipped (with tip)." : "Lesson skipped (without tip).");
            });
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Checking whether the lesson can be skipped failed: {e}");
        }
    }

    [HarmonyPatch(typeof(RidingLesson), nameof(RidingLesson.PauseLesson))]
    [HarmonyPostfix]
    private static void AfterPauseLesson()
    {
        PauseMenuSkip.EndOffer();
    }

    private static bool CanSkip(RidingLesson lesson)
    {
        var settings = SkipSettings.Current;
        // EndRide ignores lessons that aren't running (e.g. still in the tutorial).
        if (!settings.Lessons || !lesson.running)
            return false;
        var horse = lesson.horse != null ? lesson.horse.data : DataManager.i?.currentlySelectedHorse;
        if (horse == null)
            return false;
        if (horse.Trust < settings.LessonConfidence)
        {
            Plugin.Logger.LogInfo($"Skip not offered: {horse.name} has {horse.Trust}% confidence, {settings.LessonConfidence}% needed.");
            return false;
        }
        return true;
    }

    private static void FinishAsGoodLesson(RidingLesson lesson, bool tips)
    {
        // EndRide scores the lesson as correct / (correct + wrong) time per rider error; make every one correct.
        var correct = lesson.timesCorrect;
        var wrong = lesson.timesFalse;
        for (var i = 0; i < correct.Length; i++)
        {
            var missed = i < wrong.Length ? wrong[i] : 0f;
            correct[i] = Math.Max(1f, correct[i] + missed);
        }
        for (var i = 0; i < wrong.Length; i++)
            wrong[i] = 0f;

        // At least 3 correct inputs and fewer than 1 wrong per 1.5 correct pass EndRide's input check;
        // "excellent" needs more than 3 correct per wrong input, so 3:1 keeps the lesson at "good".
        lesson.numCorrectInputs = 3;
        lesson.numWrongInputs = 1;

        // EndRide pays moneyBase + moneyTipGood for a good lesson; withhold the tip for the duration of the call.
        var plan = lesson.chosenLesson;
        var tip = plan.moneyTipGood;
        if (!tips)
            plan.moneyTipGood = 0;
        try
        {
            lesson.EndRide(false);
        }
        finally
        {
            plan.moneyTipGood = tip;
        }
    }
}
