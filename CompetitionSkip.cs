using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SkipTraining;

// Skipping competitions from the ride's pause menu. A horse may skip a competition after winning its
// previous one; the skip replays the horse's best run in that same competition, which the game then
// ranks against the other riders and pays out like any other run. The game keeps no per-horse results,
// so finished competition rides are recorded here (CompetitionRecords).
[HarmonyPatch]
internal static class CompetitionSkip
{
    // Set while a skip replays a run, so the replay doesn't count as a new run of the horse.
    private static bool replaying;

    // Called while the pause dialog of a competition ride is built.
    public static void Offer(Sidescroller ride, HorseData horse)
    {
        if (!SkipSettings.Current.Competitions)
            return;
        var records = CompetitionRecords.Current;
        if (!records.WonLatest(horse.uuid))
        {
            Plugin.Logger.LogInfo($"Skip not offered: {horse.name} didn't win its previous competition.");
            return;
        }
        var run = records.Best(horse.uuid, ride.SelectedCourse.name);
        if (run == null)
        {
            Plugin.Logger.LogInfo($"Skip not offered: {horse.name} has no recorded run in this competition yet.");
            return;
        }
        PauseMenuSkip.Offer(Plugin.CompetitionButtonLabel.Value, () =>
        {
            if (ride.courseFinished)
                return;
            Replay(ride, run);
            Plugin.Logger.LogInfo($"Competition skipped with {horse.name}'s best run: {run.Describe()}.");
        });
    }

    private static void Replay(Sidescroller ride, CompetitionRun run)
    {
        // FinishCourse takes the penalties from the ride; the fault lists are what the result screen counts.
        Refill(ride.falls, run.Falls);
        Refill(ride.refusals, run.Refusals);
        Refill(ride.knocks, run.Knocks);
        Refill(ride.missedGates, run.MissedGates);
        ride.penalties = run.BasePenalties;

        // FinishCourse scores equitation as correct / (correct + wrong) time.
        if (ride.SelectedCourse.isEquitation)
        {
            var score = Math.Clamp(run.EquitationScore, 0f, 100f);
            ride.equitationCorrectTime = score;
            ride.equitationWrongTime = 100f - score;
        }

        // The ride time is measured from timestampCourseStart; time faults follow from it as usual.
        // It must stay above 0 (0 means "not started"), so shortly after launch the replayed time is shorter.
        ride.timestampCourseStart = Math.Max(Time.time - run.Time, 0.001f);

        replaying = true;
        try
        {
            // Same call the game makes when the horse crosses the finish line.
            ride.FinishCourse(true, true, "");
        }
        finally
        {
            replaying = false;
        }
    }

    private static void Refill(Il2CppSystem.Collections.Generic.List<int> faults, int count)
    {
        faults.Clear();
        for (var i = 0; i < count; i++)
            faults.Add(0);
    }

    // Courses whose results are already recorded today ("course|horse"), and each player horse's
    // prize money total before its course was paid out.
    private static readonly HashSet<string> recorded = new();
    private static readonly Dictionary<string, int> prizeBefore = new();

    [HarmonyPatch(typeof(CourseManager), nameof(CourseManager.StartCompetition))]
    [HarmonyPrefix]
    private static void BeforeStartCompetition()
    {
        recorded.Clear();
        prizeBefore.Clear();
    }

    [HarmonyPatch(typeof(Sidescroller), nameof(Sidescroller.FinishCourse))]
    [HarmonyPrefix]
    private static void BeforeFinishCourse()
    {
        try
        {
            var data = DataManager.i;
            if (data == null || data.currentlySelectedCourseType != CourseType.COMPETITION || data.data == null)
                return;
            // Includes horses kept off the property, which can compete too.
            var horses = data.data.GetListOfAllHorsesCurrentlyOwned();
            for (var i = 0; i < horses.Count; i++)
                if (!prizeBefore.ContainsKey(horses[i].uuid))
                    prizeBefore[horses[i].uuid] = horses[i].competitionPrizeMoney;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Reading the prize money before the competition failed: {e}");
        }
    }

    [HarmonyPatch(typeof(Sidescroller), nameof(Sidescroller.FinishCourse))]
    [HarmonyPostfix]
    private static void AfterFinishCourse(Sidescroller __instance, bool success, bool crossedFinishLine, string failureReason)
    {
        try
        {
            var data = DataManager.i;
            if (data == null || data.currentlySelectedCourseType != CourseType.COMPETITION || data.data == null)
                return;
            var results = data.competitionResults;
            var horse = __instance.horse != null ? __instance.horse.data : data.currentlySelectedHorse;
            if (results == null || horse == null)
                return;
            var records = CompetitionRecords.Current;

            if (!replaying && success && crossedFinishLine && string.IsNullOrEmpty(failureReason))
                RecordRun(records, __instance, horse, results);

            // Placings and prizes of a course are final once every player horse in it has ridden.
            var own = new Dictionary<string, HorseData>();
            // Includes horses kept off the property, which can compete too.
            var horses = data.data.GetListOfAllHorsesCurrentlyOwned();
            for (var i = 0; i < horses.Count; i++)
                own[horses[i].uuid] = horses[i];
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.horseUuid != null && own.ContainsKey(result.horseUuid) && !result.hasRidden)
                    return;
            }

            var course = __instance.SelectedCourse.name;
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.horseUuid == null || !own.TryGetValue(result.horseUuid, out var mine)
                    || !recorded.Add(course + "|" + result.horseUuid))
                    continue;
                var place = PlaceOf(result, results);
                var prize = prizeBefore.TryGetValue(result.horseUuid, out var before)
                    ? mine.competitionPrizeMoney - before : 0;
                prizeBefore.Remove(result.horseUuid);
                records.AddRide(result.horseUuid, course, place, result.time, prize);
                records.SetWonLatest(result.horseUuid, place == 1);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Recording the competition result failed: {e}");
        }
    }

    // 1 = first; 0 = eliminated or didn't finish. Counted with the game's own ranking.
    private static int PlaceOf(CourseManager.CompetitionResult result,
        Il2CppSystem.Collections.Generic.List<CourseManager.CompetitionResult> results)
    {
        if (!result.hasRidden || result.eliminated)
            return 0;
        // Ties go to whoever the game lists first (its result list is sorted), so only one rider is 1st.
        var place = 1;
        var before = true;
        for (var i = 0; i < results.Count; i++)
        {
            var other = results[i];
            if (other.horseUuid == result.horseUuid)
            {
                before = false;
                continue;
            }
            if (!other.hasRidden || other.eliminated)
                continue;
            var order = other.CompareTo(result);
            if (order < 0 || (order == 0 && before))
                place++;
        }
        return place;
    }

    private static void RecordRun(CompetitionRecords records, Sidescroller ride, HorseData horse,
        Il2CppSystem.Collections.Generic.List<CourseManager.CompetitionResult> results)
    {
        CourseManager.CompetitionResult result = null;
        for (var i = 0; i < results.Count; i++)
            if (results[i].horseUuid == horse.uuid)
                result = results[i];
        if (result == null || !result.hasRidden || result.eliminated)
            return;

        var run = new CompetitionRun
        {
            Time = result.time,
            BasePenalties = ride.penalties,
            Penalties = result.penalties,
            EquitationScore = result.equitationScore,
            TimeOverScore = result.timeOverScore,
            Falls = ride.falls.Count,
            Refusals = ride.refusals.Count,
            Knocks = ride.knocks.Count,
            MissedGates = ride.missedGates.Count
        };
        if (records.Offer(horse.uuid, ride.SelectedCourse.name, run))
            Plugin.Logger.LogInfo($"New best run for {horse.name} in this competition: {run.Describe()}.");
    }
}
