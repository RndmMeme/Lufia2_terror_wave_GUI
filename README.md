# Lufia II Terror Wave GUI

A native Windows front end for Abyssonym's **Terror Wave v3** randomizer. It validates supported ROM hashes, exposes the real randomizer flags and special codes, and runs the existing Windows executable without modifying it.

## Features

- ROM and randomizer path pickers with automatic sibling-folder discovery
- MD5 validation for vanilla NA, Fixxxer Deluxe, and Frue ROMs
- Standard, Open World, Four Keys, Custom Open World, and vanilla-patch modes
- All eight randomization categories
- Randomness and difficulty controls
- Automatic, forced, disabled, and split enemy scaling
- Supported fun/cheat switches from the v3 source
- Live process log, cancellation, and output-folder shortcut

## Run from source

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet run
```

The app searches parent directories for:

```text
l2_terror_wave_windows\l2_terror_wave.exe
```

You can always select the executable manually. Generated ROMs are written next to the source ROM by Terror Wave itself.

## Build a standalone folder

Framework-dependent (small, requires .NET 8 Desktop Runtime):

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Run the dependency-free command-building smoke tests with:

```powershell
dotnet run --project tests\L2TerrorWaveGui.SmokeTests -c Release
```

The GUI deliberately does not copy or redistribute the randomizer executable or copyrighted ROM data. Project details for Terror Wave are at <https://github.com/abyssonym/terrorwave>.

## Supported source ROM hashes

| ROM | MD5 |
|---|---|
| Lufia II NA (vanilla) | `6efc477d6203ed2b3b9133c1cd9e9c5d` |
| Fixxxer Deluxe | `026b649ed316448e038349e39a6fe579` |
| Frue | `b58c76f2ac0b2aeb9b779e880d2bff18` |
