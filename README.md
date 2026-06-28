# Lufia II Terror Wave GUI

A native Windows front end for Abyssonym's **Terror Wave 3.16** randomizer. The unmodified randomizer engine is embedded inside the application and extracted to the user's local application-data directory when first needed. Users do not need Python or a separate randomizer download.

## Features

- Single-file, self-contained Windows release
- Embedded Terror Wave 3.16 engine with SHA-256 integrity validation
- MD5 validation for vanilla NA, Fixxxer Deluxe, and Frue ROMs
- Automatic support for 512-byte SNES copier headers
- Standard, Open World, Four Keys, Custom Open World, and vanilla-patch modes
- All eight randomization categories and supported v3 special codes
- Randomness, difficulty, and Open World enemy-scaling controls
- Live process log, cancellation, and output-folder shortcut

## Run from source

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet run
```

The embedded engine is extracted on demand beneath:

```text
%LOCALAPPDATA%\L2TerrorWaveGui\Engines
```

Generated ROMs are written next to the selected source ROM by Terror Wave. ROM data is never bundled with the application.

## Publish the standalone application

```powershell
dotnet publish -p:PublishProfile=win-x64
```

The resulting standalone executable is written to:

```text
publish\win-x64\L2TerrorWaveGui.exe
```

It includes the .NET 8 Windows runtime and the Terror Wave engine. No separate installation is required.

## Verification

Run the dependency-free smoke tests:

```powershell
dotnet run --project tests\L2TerrorWaveGui.SmokeTests -c Release
```

An optional end-to-end test accepts a legally obtained ROM path. It copies the ROM into ignored temporary artifacts, runs both vanilla-patch and all-category seeds, validates the outputs, and removes the copies:

```powershell
dotnet run --project tests\L2TerrorWaveGui.SmokeTests -c Release -- --integration-rom "C:\path\to\Lufia II.smc"
```

## Supported source ROM hashes

Hashes are calculated after ignoring an optional 512-byte copier header.

| ROM | MD5 |
|---|---|
| Lufia II NA (vanilla) | `6efc477d6203ed2b3b9133c1cd9e9c5d` |
| Fixxxer Deluxe | `026b649ed316448e038349e39a6fe579` |
| Frue | `b58c76f2ac0b2aeb9b779e880d2bff18` |

## Upstream and redistribution

Terror Wave is by Abyssonym: <https://github.com/abyssonym/terrorwave>.

The upstream repository and supplied snapshot do not contain a top-level license file, while the `randomtools` dependency includes GPL-3.0. See [`Vendor/TerrorWave/THIRD_PARTY_NOTICE.md`](Vendor/TerrorWave/THIRD_PARTY_NOTICE.md) and confirm redistribution terms with Abyssonym before releasing the bundled executable.
