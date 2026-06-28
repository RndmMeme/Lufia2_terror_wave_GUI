# Lufia II Terror Wave GUI

> [!IMPORTANT]
> **Experimental, unofficial preview.** This build is being shared for review with Abyssonym, the creator of Terror Wave. It is not an official or endorsed Terror Wave release, and redistribution permission for the embedded engine is still being confirmed.

A native Windows front end for Abyssonym's **Terror Wave 3.16** randomizer. The unmodified randomizer engine is embedded inside the application and extracted to the user's local application-data directory when first needed. Users do not need Python or a separate randomizer download.

## Features

- Single-file, self-contained Windows release
- Embedded Terror Wave 3.16 engine with SHA-256 integrity validation
- MD5 validation for vanilla NA, Fixxxer Deluxe, and Frue ROMs
- Automatic support for 512-byte SNES copier headers
- Standard, Open World, Four Keys, Custom Open World, and vanilla-patch modes
- All eight randomization categories and supported v3 special codes
- Randomness, difficulty, and Open World enemy-scaling controls
- One organized result folder per seed, created beside the source ROM
- Live process log, cancellation, and seed-folder shortcut
- Path-independent deterministic output for identical settings and seeds

## Run from source

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet run
```

The embedded engine is extracted on demand beneath:

```text
%LOCALAPPDATA%\L2TerrorWaveGui\Engines
```

Every run creates a folder named after its seed beside the selected source ROM:

```text
<source ROM folder>\<seed>\
  <ROM name>.<flags>.<seed>.smc
  randomizer.log
  events.txt
  spoiler.txt
  run.json
```

Open World modes preserve Terror Wave's item/progression spoiler as `spoiler.txt`. Other modes receive a clearly marked seed summary because Terror Wave does not generate item-level spoilers for them. `run.json` records settings, engine details, source/output hashes, timestamps, and completion status.

The source ROM is copied temporarily into the seed folder while the engine runs and removed afterward. ROM data is never bundled with the application.

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

An optional end-to-end test accepts a legally obtained ROM path. It verifies vanilla-patch, all-category, deterministic repeat, and Open World-spoiler runs, validates the seed-folder contents, and removes its copies:

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

The repository's MIT license covers the original GUI code in this project; it does not replace or override the rights and licensing of the embedded Terror Wave engine or its dependencies.
