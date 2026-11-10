using ModManager;

namespace SkipTraining;

// Shows the skip settings in the Mod Manager's mod window, the only place they can be changed.
internal static class ModManagerLink
{
    private static readonly string[] ScopeChoices = { "Off", "Owned horses", "Boarders", "All horses" };
    private static readonly string[] OnOffChoices = { "Off", "On" };

    // Confidence sliders move in 5% steps (slider values 0..20), like in the settings menu.
    private const int Step = 5;
    private const int DefaultSteps = 10;

    private static SkipSettings S => SkipSettings.Current;

    // The four button texts stay in the .cfg file; the mod window only shows the skip settings.
    public static void Register() => ModSettings.Register(Plugin.Guid, Build, showConfigEntries: false);

    private static void Build(ISettingsPage page)
    {
        // The same for every save game, so they can be changed from the main menu too.
        page.Heading("Skip rides and lessons");
        page.Choice("Skip training", ScopeChoices, () => (int)S.Training, i => Change(() => S.Training = (HorseScope)i));
        Confidence(page, "Owned horses", () => S.TrainingOwnedConfidence, p => S.TrainingOwnedConfidence = p, () => S.Training.IncludesOwned());
        Confidence(page, "Boarders", () => S.TrainingBoarderConfidence, p => S.TrainingBoarderConfidence = p, () => S.Training.IncludesBoarders());

        page.Choice("Skip trail rides", ScopeChoices, () => (int)S.TrailRides, i => Change(() => S.TrailRides = (HorseScope)i));
        Confidence(page, "Owned horses", () => S.TrailOwnedConfidence, p => S.TrailOwnedConfidence = p, () => S.TrailRides.IncludesOwned());
        Confidence(page, "Boarders", () => S.TrailBoarderConfidence, p => S.TrailBoarderConfidence = p, () => S.TrailRides.IncludesBoarders());

        page.Choice("Skip lessons", OnOffChoices, () => S.Lessons ? 1 : 0, i => Change(() => S.Lessons = i == 1));
        page.Toggle("Skipped lessons earn tips", () => S.LessonTips, on => Change(() => S.LessonTips = on), () => S.Lessons);
        Confidence(page, "Lesson horse", () => S.LessonConfidence, p => S.LessonConfidence = p, () => S.Lessons);

        page.Choice("Skip competitions", OnOffChoices, () => S.Competitions ? 1 : 0, i => Change(() => S.Competitions = i == 1));
        page.Hint("A horse can skip a competition after winning its previous one. The skip reuses the horse's best run in that competition.",
            () => S.Competitions);

        page.Heading("Horse Info");
        page.Toggle("Results per competition", () => S.HorseInfoResults, on => Change(() => S.HorseInfoResults = on));
        page.Hint("Shows each horse's best placing, wins, best time and prize money per competition in the stable's Horse Info book.");
    }

    private static void Confidence(ISettingsPage page, string horses, System.Func<int> get, System.Action<int> set, System.Func<bool> visible) =>
        page.Slider(steps => $"{horses}: from {steps * Step}% confidence", 0, 100 / Step,
            () => (int)System.Math.Round(get() / (double)Step), steps => Change(() => set(steps * Step)),
            DefaultSteps, visible);

    private static void Change(System.Action apply)
    {
        apply();
        S.Save();
    }
}
