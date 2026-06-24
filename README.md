# Magic Deceiver Compat

A small compatibility mod for **Pathfinder: Wrath of the Righteous**.

This mod focuses on making Magic Deceiver interact more cleanly with metamagic and Loremaster progression.

## Features

- Allows Magic Deceiver's fused spells to use normal metamagic.
- Keeps Magic Deceiver's spell fusion button usable alongside metamagic mode.
- Preserves fused spell icons and spell data when applying metamagic.
- Fixes metamagic spell-level handling for fused spells.
- Lets Loremaster progress the Magic Deceiver spellbook through Arcanist spellbook selections.
- Removes the Magic Deceiver/Loremaster incompatibility caused by archetype prerequisites.

## Installation

1. Install with Unity Mod Manager, or extract the release archive into the game's `Mods` folder.
2. The installed folder should look like this:

```text
Mods/
  MagicDeceiverCompat/
    Info.json
    MagicDeceiverCompat.dll
```

Use the zip from GitHub Releases for normal play. Do not install the source-code archive.

## Compatibility Notes

- The mod is designed to load after common Wrath mods such as ToyBox, PrestigePlus, TabletopTweaks, and deceiverbuff.
- It does not replace deceiverbuff. If you already use deceiverbuff, keep it installed as usual.
- Because this mod patches UI and spellbook behavior, conflicts are possible with other mods that heavily modify Magic Deceiver, metamagic, or spellbook UI.

## Building

This project targets `.NET Framework 4.7.2`.

The `.csproj` references game assemblies from a local Pathfinder: Wrath of the Righteous installation. If your game is installed somewhere else, update the `HintPath` entries in `MagicDeceiverCompat.csproj` before building.

Expected output:

```text
bin/
  MagicDeceiverCompat.dll
  Info.json
```

## Author

Kay
