# Skip Training for Horses of Hoofprint Bay

Adds a skip choice to the pause menu of training rides, trail rides, riding lessons and competitions in [Horses of Hoofprint Bay](https://store.steampowered.com/app/3727090/). Press Escape during a ride and pick Skip training, Skip trail ride, Skip lesson or Skip competition next to Continue and Abort ride.

A skipped ride ends the way it would at the finish line, so the game applies the results itself:

- Training and trail rides count as a clean ride. The horse gets its strength, endurance and exercise as usual, and trail ride quests count.
- A skipped lesson counts as a good lesson. The client pays the lesson fee, and the good lesson tip if you allow it.
- A horse can skip a competition after it won its previous one. The skip replays the horse's best run in that competition, and the game ranks it against the other riders and pays the prize as usual. The first time in a competition, the horse has to ride it.

The mod also adds results per competition to the Horse Info book in the stable. Each discipline (Caprilli, Showjumping, Cross Country) opens to show the horse's best placing, number of wins, best time and prize money for every level. These are counted from when you install the mod. For older results the mod uses the horse's competition level, so a level the horse won shows "1+ wins".

## Settings

The settings are in the Mods menu of [Mod Manager](https://github.com/LaylasLethalParadox/HoofprintBay-ModManager), under Skip Training. They are the same for every save.

- Skip training and Skip trail rides: off, owned horses, boarders or all horses. A slider for owned horses and one for boarders set the confidence a horse needs before you can skip.
- Skip lessons: on or off, whether a skipped lesson earns the tip, and the confidence the lesson horse needs.
- Skip competitions: on or off.
- Results per competition: shows or hides the results in Horse Info.

The texts of the four skip choices can be changed in `BepInEx\config\hoofprintbay.skiptraining.cfg`.

## Install

You need BepInEx 6 bleeding edge build 793 for IL2CPP (Windows x64) and Mod Manager. BepInEx 5 and the older 6.0.0-pre builds can't load this game.

1. Install BepInEx and Mod Manager as described on the [Mod Manager page](https://github.com/LaylasLethalParadox/HoofprintBay-ModManager#install).
2. Extract the Skip Training zip into the game folder, so that you have `BepInEx\plugins\SkipTraining\SkipTraining.dll`.
3. Start the game. The skip settings are in the Mods menu.

To uninstall, delete `BepInEx\plugins\SkipTraining`.

## Saves

Skip Training doesn't change `save.json`. It keeps each horse's competition results in a file called `SkipTraining.competitions` in the save's folder, and writes it when the game saves. If you delete that file, competition skips have to be earned again and the results in Horse Info start over.

## Building

You need the .NET 6 SDK, the game with BepInEx installed and started at least once, and Mod Manager installed, because the project compiles against files from both. This repository contains no game files.

```
dotnet build -c Release -p:GameDir="C:\Path\To\HorsesOfHoofprintBay\\"
```

The build copies the DLL into the game's `BepInEx\plugins\SkipTraining` folder, so close the game first. `package.ps1` makes the release zip.

## License

GPL-3.0, see [LICENSE](LICENSE).

This is an unofficial fan project and isn't affiliated with or endorsed by the developers of Horses of Hoofprint Bay.
