# WastedMods
Here you can find 3 mods I made for the game WASTED

## How to install

Copy Folders in this github to Wasted\Mods, for one of the mods you also need Binge Edition (I intend on changing that soon), for the others you need to change one of the game's DLL files (the mods are actually in the DLL, the mod just enables the feature)

## CourierRun6 
Allows for 6 Courier Slots in Courier Run (requires the mod Binge Edition to work)
## KeepHangoverOnDeath
Allows for keeping Hangovers when you die (requires to change the Assembly-CSharp.dll on the folder Wasted\Wasted_Data\Managed for the one on https://github.com/ggvaldez/WastedMods/)
## StashInPracticeRuns
Alows you to stash items in courier practice runs (requires to change the Assembly-CSharp.dll on the folder Wasted\Wasted_Data\Managed for the one on https://github.com/ggvaldez/WastedMods/)

## New Classes and Variables for Scripting
AddTimeBeforeHuntAction - Adds (or removes) time to S.O.B.

usage:
```
component = child_gameobject.AddComponent("AddTimeBeforeHuntAction")
component.time_change = 90
```
Globals._canStashInPractice - Allows for stashing items in Practice Courier

usage:
```
GetGlobals()._canStashInPractice = true
```

Globals.keepHangoversOnDeath

usage:
```
GetGlobals().keepHangoversOnDeath = true
```
HaltOnInfiniCoolerAction - Allows for checking if player is in Courier Run

usage:
```
component = child_gameobject.AddComponent("HaltOnInfiniCoolerAction")
```

For more info on how scripts work on the game, I recommend taking a look at the code used in Binge Edition (by king bore haha) available to download on https://steamcommunity.com/app/327510/discussions/3/1369506834139485716/
