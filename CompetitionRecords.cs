using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SkipTraining;

// One finished competition run, with the values the game ranks it by.
internal class CompetitionRun
{
    public float Time;
    public int BasePenalties;     // faults during the ride: 4 per fall, refusal, knock or missed gate
    public int Penalties;         // including time faults, as ranked
    public float EquitationScore;
    public bool TimeOverScore;    // ties go to the faster time instead of the higher equitation score
    public int Falls, Refusals, Knocks, MissedGates;

    // Same order as CourseManager.CompetitionResult.CompareTo for riders who weren't eliminated.
    public bool IsBetterThan(CompetitionRun other)
    {
        if (Penalties != other.Penalties)
            return Penalties < other.Penalties;
        return TimeOverScore ? Time < other.Time : EquitationScore > other.EquitationScore;
    }

    public string Describe() => TimeOverScore
        ? $"{Time:0.00} s, {Penalties} penalties"
        : $"{Time:0.00} s, {Penalties} penalties, {EquitationScore:0.#}% equitation";

    public string Serialize() => string.Join(";",
        Time.ToString("R", CultureInfo.InvariantCulture),
        BasePenalties.ToString(CultureInfo.InvariantCulture),
        Penalties.ToString(CultureInfo.InvariantCulture),
        EquitationScore.ToString("R", CultureInfo.InvariantCulture),
        TimeOverScore.ToString(),
        Falls.ToString(CultureInfo.InvariantCulture),
        Refusals.ToString(CultureInfo.InvariantCulture),
        Knocks.ToString(CultureInfo.InvariantCulture),
        MissedGates.ToString(CultureInfo.InvariantCulture));

    public static CompetitionRun Parse(string text)
    {
        var parts = text.Split(';');
        if (parts.Length != 9)
            return null;
        try
        {
            return new CompetitionRun
            {
                Time = float.Parse(parts[0], CultureInfo.InvariantCulture),
                BasePenalties = int.Parse(parts[1], CultureInfo.InvariantCulture),
                Penalties = int.Parse(parts[2], CultureInfo.InvariantCulture),
                EquitationScore = float.Parse(parts[3], CultureInfo.InvariantCulture),
                TimeOverScore = bool.Parse(parts[4]),
                Falls = int.Parse(parts[5], CultureInfo.InvariantCulture),
                Refusals = int.Parse(parts[6], CultureInfo.InvariantCulture),
                Knocks = int.Parse(parts[7], CultureInfo.InvariantCulture),
                MissedGates = int.Parse(parts[8], CultureInfo.InvariantCulture)
            };
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return null;
        }
    }
}

// A horse's results in one competition course, counted from when Skip Training is installed.
internal class CourseStats
{
    public int BestPlace;       // 0 = never finished
    public int Rides;
    public float BestTime = -1; // seconds, -1 = none
    public int Prize;
    public int Wins;

    public string Serialize() => string.Join(";",
        BestPlace.ToString(CultureInfo.InvariantCulture),
        Rides.ToString(CultureInfo.InvariantCulture),
        BestTime.ToString("R", CultureInfo.InvariantCulture),
        Prize.ToString(CultureInfo.InvariantCulture),
        Wins.ToString(CultureInfo.InvariantCulture));

    // Lines written before wins were counted have 4 parts.
    public static CourseStats Parse(string text)
    {
        var parts = text.Split(';');
        if (parts.Length != 4 && parts.Length != 5)
            return null;
        try
        {
            return new CourseStats
            {
                BestPlace = int.Parse(parts[0], CultureInfo.InvariantCulture),
                Rides = int.Parse(parts[1], CultureInfo.InvariantCulture),
                BestTime = float.Parse(parts[2], CultureInfo.InvariantCulture),
                Prize = int.Parse(parts[3], CultureInfo.InvariantCulture),
                // An old line with a first place proves at least one win.
                Wins = parts.Length == 5 ? int.Parse(parts[4], CultureInfo.InvariantCulture)
                    : parts[0] == "1" ? 1 : 0
            };
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return null;
        }
    }
}

// Competition history the game doesn't keep: each horse's best run per competition, whether the
// horse won its latest competition, and its results per course. Kept per save game, next to its save.json.
internal class CompetitionRecords
{
    private const string FileName = "SkipTraining.competitions";
    private const string WonPrefix = "won|";
    private const string BestPrefix = "best|";
    private const string StatsPrefix = "stats|";

    // Horse uuid -> won its latest competition.
    private readonly Dictionary<string, bool> wonLatest = new();
    // "horse uuid|competition name" -> best run.
    private readonly Dictionary<string, CompetitionRun> best = new();
    // "horse uuid|competition name" -> results.
    private readonly Dictionary<string, CourseStats> stats = new();
    // Lines this version can't read (e.g. from a newer version); written back unchanged.
    private readonly List<string> unknown = new();
    private string save;
    private bool changed;
    private static CompetitionRecords current;

    // Written to disk only when the game saves, so a crash or quitting without saving undoes both together.
    public static void SaveWithGame()
    {
        if (current != null && current.changed)
            current.Write();
    }

    // Unsaved changes are dropped when a save is (re)loaded or deleted, like the game's own.
    public static void Forget() => current = null;

    public static CompetitionRecords Current
    {
        get
        {
            var loaded = SaveFiles.LoadedSave;
            if (current == null || current.save != loaded)
                current = Load(loaded);
            return current;
        }
    }

    private static string Key(string horse, string competition) => horse + "|" + competition;

    public bool WonLatest(string horse) => wonLatest.TryGetValue(horse, out var won) && won;

    public CompetitionRun Best(string horse, string competition) =>
        best.TryGetValue(Key(horse, competition), out var run) ? run : null;

    public CourseStats Stats(string horse, string competition) =>
        stats.TryGetValue(Key(horse, competition), out var s) ? s : null;

    // place: 1 = won, 0 = eliminated or didn't finish. time is ignored when place is 0.
    public void AddRide(string horse, string competition, int place, float time, int prize)
    {
        var key = Key(horse, competition);
        if (!stats.TryGetValue(key, out var s))
            stats[key] = s = new CourseStats();
        s.Rides++;
        s.Prize += Math.Max(prize, 0);
        if (place == 1)
            s.Wins++;
        if (place > 0)
        {
            if (s.BestPlace == 0 || place < s.BestPlace)
                s.BestPlace = place;
            if (time > 0f && (s.BestTime < 0f || time < s.BestTime))
                s.BestTime = time;
        }
        changed = true;
    }

    public void SetWonLatest(string horse, bool won)
    {
        wonLatest[horse] = won;
        changed = true;
    }

    // Keeps the run when it beats the horse's best in that competition; returns whether it did.
    public bool Offer(string horse, string competition, CompetitionRun run)
    {
        var key = Key(horse, competition);
        if (best.TryGetValue(key, out var previous) && !run.IsBetterThan(previous))
            return false;
        best[key] = run;
        changed = true;
        return true;
    }

    private static CompetitionRecords Load(string name)
    {
        var records = new CompetitionRecords { save = name };
        if (!SaveFiles.IsSave(name))
            return records;
        var path = SaveFiles.PathFor(name, FileName);
        if (!File.Exists(path))
            return records;
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var separator = line.IndexOf('=');
                if (line.StartsWith("#") || separator <= 0)
                    continue;
                var key = line.Substring(0, separator);
                var value = line.Substring(separator + 1);
                if (key.StartsWith(WonPrefix) && bool.TryParse(value, out var won))
                    records.wonLatest[key.Substring(WonPrefix.Length)] = won;
                else if (key.StartsWith(BestPrefix) && CompetitionRun.Parse(value) is { } run)
                    records.best[key.Substring(BestPrefix.Length)] = run;
                else if (key.StartsWith(StatsPrefix) && CourseStats.Parse(value) is { } s)
                    records.stats[key.Substring(StatsPrefix.Length)] = s;
                else
                    records.unknown.Add(line);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Reading {path} failed: {e.Message}");
        }
        return records;
    }

    private void Write()
    {
        if (!SaveFiles.IsSave(save))
            return;
        var path = SaveFiles.PathFor(save, FileName);
        try
        {
            var lines = new List<string>
            {
                "# Skip Training mod: competition results per horse (time;penalties without and with time faults;equitation score;",
                "# ties by time;falls;refusals;knocks;missed gates), whether each horse won its latest competition,",
                "# and per course: best place (0 = none);rides;best time (-1 = none);prize money;wins."
            };
            foreach (var entry in wonLatest)
                lines.Add($"{WonPrefix}{entry.Key}={entry.Value}");
            foreach (var entry in best)
                lines.Add($"{BestPrefix}{entry.Key}={entry.Value.Serialize()}");
            foreach (var entry in stats)
                lines.Add($"{StatsPrefix}{entry.Key}={entry.Value.Serialize()}");
            lines.AddRange(unknown);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Write a copy first, so a crash mid-write can't leave a half file behind.
            var temp = path + ".tmp";
            File.WriteAllLines(temp, lines);
            File.Move(temp, path, true);
            changed = false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Writing {path} failed: {e.Message}");
        }
    }
}
