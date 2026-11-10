using System.IO;

namespace SkipTraining;

// Files the mod keeps per save game, in the save's folder next to save.json.
internal static class SaveFiles
{
    private const string NoSave = "NoSave";

    // Folder name of the loaded save game; "NoSave" while none is loaded.
    public static string LoadedSave => DataManager.currentSaveDirectory;

    public static bool IsSave(string name) => !string.IsNullOrEmpty(name) && name != NoSave;

    // The game treats "save*.json" files in the save folder as backups, so file names must stay clear of that pattern.
    public static string PathFor(string save, string fileName)
    {
        var folder = Path.Combine(DataManager.saveGamePath ?? "", save);
        if (Directory.Exists(folder))
            return Path.Combine(folder, fileName);
        // Saves outside the regular save folder keep their files with the plugin config.
        return Path.Combine(BepInEx.Paths.ConfigPath, "SkipTraining", save, fileName);
    }
}
