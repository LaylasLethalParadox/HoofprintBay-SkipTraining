using System;
using HarmonyLib;
using UnityEngine;

namespace SkipTraining;

// Skipping training and trail rides from the ride's pause menu (competitions: CompetitionSkip). Skipping
// finishes the ride the same way crossing the finish line does, so the game itself applies the results:
// strength/endurance gain, exercise, statistics, achievements and the trail-ride quest.
[HarmonyPatch]
internal static class RideSkip
{
    // FinishCourse reveals some personalities only for rides longer than 30 seconds.
    private const float MinRideSeconds = 31f;

    [HarmonyPatch(typeof(Sidescroller), nameof(Sidescroller.PauseRiding))]
    [HarmonyPrefix]
    private static void BeforePauseRiding(Sidescroller __instance)
    {
        PauseMenuSkip.EndOffer();
        try
        {
            var data = DataManager.i;
            if (data != null && data.currentlySelectedCourseType == CourseType.COMPETITION)
            {
                var horse = __instance.horse != null ? __instance.horse.data : data.currentlySelectedHorse;
                if (!__instance.courseFinished && horse != null)
                    CompetitionSkip.Offer(__instance, horse);
                return;
            }

            var label = SkipLabelFor(__instance);
            if (label == null)
                return;
            var ride = __instance;
            var trail = DataManager.i.currentlySelectedCourseType == CourseType.TRAIL;
            PauseMenuSkip.Offer(label, () =>
            {
                if (ride.courseFinished)
                    return;
                FinishAsCleanRide(ride);
                Plugin.Logger.LogInfo(trail ? "Trail ride skipped." : "Training skipped.");
            });
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Checking whether the ride can be skipped failed: {e}");
        }
    }

    [HarmonyPatch(typeof(Sidescroller), nameof(Sidescroller.PauseRiding))]
    [HarmonyPostfix]
    private static void AfterPauseRiding()
    {
        PauseMenuSkip.EndOffer();
    }

    // Label of the skip choice for this ride, or null when the save's settings don't allow skipping it.
    private static string SkipLabelFor(Sidescroller ride)
    {
        if (ride.courseFinished)
            return null;
        var data = DataManager.i;
        if (data == null)
            return null;
        var type = data.currentlySelectedCourseType;
        if (type != CourseType.TRAINING && type != CourseType.TRAIL)
            return null;
        var horse = ride.horse != null ? ride.horse.data : data.currentlySelectedHorse;
        if (horse == null)
            return null;

        var settings = SkipSettings.Current;
        var training = type == CourseType.TRAINING;
        var boarder = horse.IsLiveryHorse();
        var scope = training ? settings.Training : settings.TrailRides;
        if (!scope.Includes(boarder))
            return null;
        var needed = training
            ? boarder ? settings.TrainingBoarderConfidence : settings.TrainingOwnedConfidence
            : boarder ? settings.TrailBoarderConfidence : settings.TrailOwnedConfidence;
        if (horse.Trust < needed)
        {
            Plugin.Logger.LogInfo($"Skip not offered: {horse.name} has {horse.Trust}% confidence, {needed}% needed.");
            return null;
        }
        return training ? Plugin.TrainingButtonLabel.Value : Plugin.TrailRideButtonLabel.Value;
    }

    private static void FinishAsCleanRide(Sidescroller ride)
    {
        var course = ride.SelectedCourse;

        // FinishCourse turns these into failure reasons (e.g. 2 falls, 3 refusals); a skipped ride is clean.
        ride.falls.Clear();
        ride.refusals.Clear();
        ride.knocks.Clear();
        ride.missedGates.Clear();

        // Equitation courses fail below a 70% score; give the skipped ride a perfect one.
        if (course.isEquitation)
        {
            ride.equitationCorrectTime = Math.Max(1f, ride.equitationCorrectTime + ride.equitationWrongTime);
            ride.equitationWrongTime = 0f;
        }

        // The ride time is measured from timestampCourseStart, which stays 0 until the horse first moves.
        // Report a full-length ride, but stay inside the course's time limit so it doesn't count as too slow.
        var now = Time.time;
        var ridden = ride.timestampCourseStart > 0f ? now - ride.timestampCourseStart : 0f;
        var duration = Math.Max(ridden, MinRideSeconds);
        var limit = course.TimeLimit;
        if (limit > 0f)
            duration = Math.Min(duration, limit * 0.9f);
        ride.timestampCourseStart = Math.Max(now - duration, 0.001f);

        // Same call the game makes when the horse crosses the finish line.
        ride.FinishCourse(true, true, "");
    }
}
