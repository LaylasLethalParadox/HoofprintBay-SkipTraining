using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SkipTraining;

[BepInPlugin(Guid, Name, Version)]
// The skip settings are changed in the Mod Manager's mod window, so it is required (1.2.0 has the number boxes).
[BepInDependency("hoofprintbay.modmanager", ">=1.2.0")]
public class Plugin : BasePlugin
{
    public const string Guid = "hoofprintbay.skiptraining";
    public const string Name = "Skip Training";
    public const string Version = "1.1.0";

    internal static ManualLogSource Logger;
    internal static ConfigEntry<string> TrainingButtonLabel;
    internal static ConfigEntry<string> TrailRideButtonLabel;
    internal static ConfigEntry<string> LessonButtonLabel;
    internal static ConfigEntry<string> CompetitionButtonLabel;

    public override void Load()
    {
        Logger = Log;

        // What may be skipped is set in the Mods menu (same for all saves, see SkipSettings); only the texts live here.
        TrainingButtonLabel = Config.Bind("Pause menu", "TrainingButton", "Skip training",
            "Text of the skip choice in the pause menu of training rides.");
        TrailRideButtonLabel = Config.Bind("Pause menu", "TrailRideButton", "Skip trail ride",
            "Text of the skip choice in the pause menu of trail rides.");
        LessonButtonLabel = Config.Bind("Pause menu", "LessonButton", "Skip lesson",
            "Text of the skip choice in the pause menu of riding lessons.");
        CompetitionButtonLabel = Config.Bind("Pause menu", "CompetitionButton", "Skip competition",
            "Text of the skip choice in the pause menu of competition rides.");

        var harmony = new Harmony(Guid);
        harmony.PatchAll(typeof(PauseMenuSkip));
        harmony.PatchAll(typeof(RideSkip));
        harmony.PatchAll(typeof(CompetitionSkip));
        harmony.PatchAll(typeof(LessonSkip));
        harmony.PatchAll(typeof(HorseBookResults));
        harmony.PatchAll(typeof(GameSaveSync));
        harmony.PatchAll(typeof(LessonRequirements));
        ModManagerLink.Register();
        Log.LogInfo("Skip choices are added to the pause menus of training, trail rides, lessons and competitions; set them in the Mods menu.");
    }
}
