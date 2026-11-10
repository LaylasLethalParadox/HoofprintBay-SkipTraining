using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SkipTraining;

// Adds a collapsible list below the text of the Horse Info book in the stable: one heading per discipline
// that opens to show each level's best placing, best time and prize money.
[HarmonyPatch]
internal static class HorseBookResults
{
    private const string Prefix = "SkipTraining Competition";
    private const string Coin = "<sprite name=icons-text_coin>";
    private const int PodiumPlaces = 3;
    // Column starts, as a share of the book's text width.
    private const string PosPlacing = "<pos=22%>", PosWins = "<pos=42%>", PosTime = "<pos=58%>", PosPrize = "<pos=77%>";
    private static readonly string[] Levels = { "Beginner", "Novice", "Intermediate", "Advanced" };
    private static readonly Discipline[] Disciplines = { Discipline.CAPRILLI, Discipline.SHOWJUMPING, Discipline.CROSSCOUNTRY };

    // What is open stays open for every horse until the game is closed.
    private static readonly HashSet<Discipline> openDisciplines = new();

    [HarmonyPatch(typeof(UiStable), nameof(UiStable.OpenHorseBook))]
    [HarmonyPostfix]
    private static void AfterOpenHorseBook(UiStable __instance) => Refresh(__instance);

    [HarmonyPatch(typeof(UiStable), nameof(UiStable.UpdateUi))]
    [HarmonyPostfix]
    private static void AfterUpdateUi(UiStable __instance)
    {
        if (__instance.horseBook != null && __instance.horseBook.activeInHierarchy)
            Refresh(__instance);
    }

    private static void Refresh(UiStable ui)
    {
        try
        {
            var info = ui.textHorseBook;
            var template = ui.textHorseBookOffspring;
            if (info == null || template == null)
                return;
            // Foals don't compete.
            var horse = ui.horseBookDisplayFoal ? null : ui.horseData;
            var at = info.transform.GetSiblingIndex();

            var open = horse != null && SkipSettings.Current.HorseInfoResults;
            // Switched off before the book was ever opened: nothing to add or hide.
            if (!open && template.transform.parent.Find(Prefix + " Note") == null)
                return;

            var header = Line(ui, template, "Header", ref at, null);
            header.text = $"<i>{PosPlacing}Placing{PosWins}Wins{PosTime}Best time{PosPrize}Prize money</i>";
            header.gameObject.SetActive(open && openDisciplines.Count > 0);

            foreach (var discipline in Disciplines)
            {
                var d = discipline;
                var shown = openDisciplines.Contains(d);
                var heading = Line(ui, template, "Heading " + d, ref at,
                    () => { if (!openDisciplines.Remove(d)) openDisciplines.Add(d); });
                heading.text = $"{(shown ? "▼" : "▶")} <b>{DisciplineName(d)}</b>";
                heading.gameObject.SetActive(open);
                var rows = Line(ui, template, "Rows " + d, ref at, null);
                rows.gameObject.SetActive(open && shown);
                if (open && shown)
                    rows.text = BuildRows(horse, d);
            }

            var note = Line(ui, template, "Note", ref at, null);
            note.text = "<size=80%>Placings, wins, times and prize money are counted from when Skip Training was installed. "
                + "Earlier wins and placings come from the horse's competition level (\"1+ wins\" = won at least once), earlier times from its best recorded runs.</size>";
            note.gameObject.SetActive(open);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Showing the competition results in the horse book failed: {e}");
        }
    }

    // The text line with this name right after the previous one, made on first use from a copy of the
    // book's offspring text (same font, size and margins). Lines with a click action toggle and redraw.
    private static TMP_Text Line(UiStable ui, TMP_Text template, string name, ref int at, Action onClick)
    {
        var parent = template.transform.parent;
        var child = parent.Find(Prefix + " " + name);
        TMP_Text text;
        if (child != null)
            text = child.GetComponent<TMP_Text>();
        else
        {
            var copy = Object.Instantiate(template.gameObject, parent);
            copy.name = Prefix + " " + name;
            foreach (var updater in copy.GetComponentsInChildren<LanguageUpdater>(true))
                Object.Destroy(updater);
            text = copy.GetComponent<TMP_Text>();
            if (onClick != null)
            {
                text.raycastTarget = true;
                // A Button handles only clicks, so scrolling over the line still scrolls the book.
                var button = copy.AddComponent<Button>();
                button.targetGraphic = text.GetComponent<Graphic>();
                button.transition = Selectable.Transition.None;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(new Action(() =>
                {
                    onClick();
                    Refresh(ui);
                }));
            }
        }
        text.transform.SetSiblingIndex(++at);
        return text;
    }

    private static string BuildRows(HorseData horse, Discipline discipline)
    {
        var courses = CourseManager.i != null ? CourseManager.i.competitionCourses : null;
        if (courses == null)
            return "";
        var records = CompetitionRecords.Current;
        var experience = Experience(horse, discipline);
        var sb = new StringBuilder();
        for (var i = 0; i < courses.Length; i++)
        {
            var course = courses[i];
            if (course == null || course.discipline != discipline)
                continue;
            // A course's ridingLevel is the placing it awards: PLACED_1 = Beginner ... PLACED_4 = Advanced.
            var offset = (int)course.ridingLevel - (int)RidingLevel.PLACED_1;
            var level = offset >= 0 ? offset / 2 : -1;
            var stats = records.Stats(horse.uuid, course.name);
            var run = records.Best(horse.uuid, course.name);
            if (sb.Length > 0)
                sb.Append('\n');
            var placing = Placing(stats, experience, level);
            sb.Append("    ").Append(level >= 0 && level < Levels.Length ? Levels[level] : course.name).Append(':')
                .Append(PosPlacing).Append(placing)
                .Append(PosWins).Append(Wins(stats, placing))
                .Append(PosTime).Append(BestTime(stats, run))
                .Append(PosPrize).Append(stats != null && stats.Rides > 0 ? $"{stats.Prize} {Coin}" : "–");
        }
        return sb.ToString();
    }

    // Best of what was counted and what the horse's competition level proves.
    private static string Placing(CourseStats stats, RidingLevel experience, int level)
    {
        // RidingLevel goes PLACED_1, WON_1, PLACED_2, ... from 3 on, two values per course level.
        var placedHere = (int)RidingLevel.PLACED_1 + 2 * level;
        // Unknown course level: only what was counted.
        var exp = level >= 0 ? (int)experience : int.MinValue;
        var counted = stats != null ? stats.BestPlace : 0;
        // Entering the next level needs a win at this one, so any higher level means "Won" here.
        if (counted == 1 || exp == placedHere + 1 || exp > placedHere + 1)
            return "Won";
        if (counted > 1 && counted <= PodiumPlaces)
            return $"Placed ({Ordinal(counted)})";
        if (exp == placedHere)
            return "Placed";
        if (stats != null && stats.Rides > 0)
            return "Not placed";
        return "–";
    }

    // Wins counted since the mod was installed; a win known only from the competition level shows as "1+".
    private static string Wins(CourseStats stats, string placing)
    {
        var wins = stats != null ? stats.Wins : 0;
        if (wins > 0)
            return wins == 1 ? "1 win" : $"{wins} wins";
        return placing == "Won" ? "1+ wins" : "–";
    }

    private static string BestTime(CourseStats stats, CompetitionRun run)
    {
        var time = stats != null ? stats.BestTime : -1f;
        if (run != null && run.Time > 0f && (time < 0f || run.Time < time))
            time = run.Time;
        if (time < 0f)
            return "–";
        // Same format as the game's result list (01:10,98).
        var total = (int)Math.Round(time * 100f);
        return $"{total / 6000:00}:{total / 100 % 60:00},{total % 100:00}";
    }

    private static RidingLevel Experience(HorseData horse, Discipline discipline)
    {
        var levels = horse.experiencePerDiscipline;
        var index = (int)discipline;
        if (levels == null)
            return RidingLevel.NONE;
        // Indexed by Discipline, or without the NONE entry.
        if (levels.Length > (int)Discipline.CROSSCOUNTRY)
            return levels[index];
        return index - 1 < levels.Length ? levels[index - 1] : RidingLevel.NONE;
    }

    private static string DisciplineName(Discipline discipline) => discipline switch
    {
        Discipline.CAPRILLI => "Caprilli",
        Discipline.SHOWJUMPING => "Showjumping",
        Discipline.CROSSCOUNTRY => "Cross Country",
        _ => discipline.ToString()
    };

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => n.ToString(CultureInfo.InvariantCulture) + "th"
    };
}
