using System;
using HarmonyLib;

namespace SkipTraining;

// Skipping riding lessons from the lesson's pause menu. A skipped lesson counts as a "good" lesson:
// the client pays the lesson fee plus the tip chosen in the settings (none, good-lesson or excellent-lesson tip).
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
                var tip = SkipSettings.Current.LessonTip;
                var paid = FinishAsGoodLesson(lesson, tip);
                Plugin.Logger.LogInfo($"Lesson skipped ({tip} tip: {paid}).");
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

    private static int FinishAsGoodLesson(RidingLesson lesson, LessonTip tipChoice)
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

        // EndRide pays moneyBase + moneyTipGood for a good lesson; swap in the chosen tip for the duration of the call.
        // Scoring the lesson "excellent" instead would change more than the money.
        var plan = lesson.chosenLesson;
        var goodTip = plan.moneyTipGood;
        var paid = tipChoice switch
        {
            LessonTip.None => 0,
            LessonTip.Best => Math.Max(goodTip, plan.moneyTipExcellent),
            _ => goodTip
        };
        plan.moneyTipGood = paid;
        try
        {
            lesson.EndRide(false);
        }
        finally
        {
            plan.moneyTipGood = goodTip;
        }
        return paid;
    }
}
