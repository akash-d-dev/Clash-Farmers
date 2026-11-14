# Repository structure

Clash Farmer is a Windows Forms app built with C# and .NET Framework. Windows
PowerShell 5.1 hosts Windows OCR and supplies the build and diagnostic scripts.
There is no package manager or project-file dependency: the scripts compile the
C# files directly.

## Start here

- **UI and saved controls:** `src/UI/MainForm.cs`
- **Battle flow, timing, and recovery:** `src/Automation/Runner.cs`
- **Troop, spell, hero, and siege positions:** `src/Placement/DeploymentPlanner.cs`
- **Button and loot recognition:** `src/Recognition/ScreenReader.cs`
- **Background Android input:** `src/Platforms/AndroidGame.cs` and `AdbClient.cs`
- **Settings defaults and migrations:** `src/Configuration/Settings.cs`

Paths below are relative to the repository root.

## Directory map

```text
Clash Farmer/
  Start.cmd                    Desktop launcher; uses the executable if built
  Start.ps1                    Wrapper for the PowerShell launcher and -SelfTest
  Build.ps1                    Wrapper for the executable build
  src/
    Program.cs                 Executable entry point and private OCR host
    App.cs                     Shared WinForms startup and host callbacks
    Automation/
      GameContracts.cs         Game interfaces, coordinates, readings, observations
      Runner.cs                Battle state machine and scheduled actions
    Configuration/
      Settings.cs              Preferences, defaults, validation, migrations
    Infrastructure/
      LocalData.cs             Local paths, first-run setup, recovery records
    Placement/
      DeploymentPlanner.cs     Side counts and troop/spell/support positions
      BattlefieldBoundary.cs   Per-opponent outer-rim fitting from screenshots
      PlacementPreview.cs      Colored troop/spell preview overlay
    Platforms/
      CapturedGame.cs          Shared screenshot-backed game adapter
      AdbClient.cs             ADB discovery, commands, screenshots, input
      AndroidGame.cs           Android adapter, guards, swipes, Back recovery
      WindowsDesktop.cs        Window discovery, viewport, focus, native input
    Recognition/
      Vision.cs                Image regions, text matching, number/pixel helpers
      HomeVillageDetector.cs   Visual Battle/Build/Shop matching
      ScreenReader.cs          OCR reads, button detection, loot, scene checks
    UI/
      MainForm.cs              Controls, settings persistence, status, run lifecycle
      ActionOrderForm.cs       Move-up/down editor for deployment action order
  scripts/
    Paths.ps1                  Shared absolute paths and C# source discovery
    Start.ps1                  Compile shared sources and launch, or run SelfTest
    Build.ps1                  Compile the executable and compatibility copy
    Ocr.ps1                    Windows OCR bridge used by both launch paths
  config/
    settings.example.json      Shareable first-run configuration
  assets/                      Runtime recognition templates and reference preview
  tests/
    *.cs                       Fake-game state-machine and recovery checks
    *.ps1                      Offline OCR, placement, UI, settings, and ADB checks
    fixtures/                  Fixed screenshots used by offline checks
  tools/                       Manual inspection, previews, and opt-in live checks
  android/                     Source and rebuild notes for the ADB pinch helper
  docs/
    ARCHITECTURE.md             This guide
  bin/                         Generated executables; ignored by Git
  data/                        Local settings, logs, captures, diagnostics; ignored
```

## How the components connect

1. **Startup:** `Program.cs` starts a private PowerShell runspace (an embedded
   PowerShell session), loads `scripts/Ocr.ps1`, and passes an OCR callback to
   `App.Run`. The script launcher supplies the same callback without using the
   executable host. `App.cs` starts `MainForm`.
2. **UI:** `MainForm` loads preferences, creates the selected platform adapter,
   and advances `Runner` through its timer. It owns the controls, taskbar title,
   logs, and Stop handling; the action-order dialog only edits the sequence.
3. **Orchestration:** `Runner` owns progress and the battle state machine: home,
   navigation, scouting/loot filtering, deployment, results, and recovery. It
   schedules actions and checks cancellation and the attack cutoff. This stays
   in one file so transitions and their shared counters can be read together.
4. **Recognition:** game adapters provide screenshots to `ScreenReader`, which
   uses the OCR callback, `Vision` rules, and `HomeVisual` templates. OCR means
   turning screenshot text into characters. Recognition returns observations;
   it does not send clicks.
5. **Placement:** `Plan` calculates balanced side counts and deployment points.
   `BattlefieldRim` can adjust preset paths to a visible outer boundary while
   preserving custom paths. `PlacementPreview` draws the same plans for review.
6. **Input:** `Runner` sends actions through `IGame` and its optional capability
   interfaces. `AndroidGame` uses `AdbClient` for background screenshots and Android
   input. ADB is the Android Debug Bridge connection to the emulator.
   `DesktopGame` uses `Native` Windows APIs and requires the game's focus.

`GameContracts.cs` keeps those boundaries explicit. For example, `IRecoveryGame`
adds Android Back and one-frame observations, while `ISwipeGame` adds swipe input.
`IZoomGame` adds scene checks and one zoom-out step; `Runner` schedules three
steps at run start and after recovery. `AdbClient` uploads and verifies the bundled
`assets/android-zoom.dex` helper once per connection, then runs it through Android's
`app_process`. It injects a genuine two-finger pinch without Windows mouse input,
root access, an installed APK, or a persistent service. Desktop zoom retains the
existing focus and viewport guards and uses the native mouse wheel.
Offline fake games implement the same interfaces without touching an emulator.

The C# namespace remains **`ClashPekkas`** for compatibility with the existing
PowerShell integration. Folder names describe responsibility; moving files does
not change persisted JSON fields or the game's input mappings.

## Settings and generated data

`Settings.cs` defines defaults, validates values, and migrates older JSON.
`MainForm` reads and saves `data/settings.json`. On first launch, `AppFiles`
copies `config/settings.example.json` only if local settings do not exist.
`RecoveryDiagnostics` writes timestamped screenshot/JSON records and retains
the latest 20 recovery records.

Keep personal settings, logs, generated previews, and executables out of Git.
Reusable test screenshots belong in `tests/fixtures/`; recognition templates
needed at runtime belong in `assets/`.

## Building and checking changes

Run from the repository root on Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-All.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Build.ps1
```

The offline suite runs each check in a fresh Windows PowerShell process. It
checks runner transitions, cancellation/recovery, recognition and loot fixtures,
placement, settings/UI, and fake ADB transport. It sends no live game input.
Outputs go to `data/test-results/` or an isolated temporary directory.

`scripts/Paths.ps1` discovers all C# files under `src/`, excluding `Program.cs`.
Startup, tests, and tools compile that shared `$SourcePaths` list. The executable
build adds `Program.cs`, which needs the PowerShell hosting assembly. New component
files under `src/` are included automatically; keep test-only C# under `tests/`.
The build writes `bin/ClashFarmer.exe` plus the legacy `ClashPekkas.exe` copy.

`tools/Verify-Startup.ps1` and `tools/Run-OneBattle.ps1` are **live, opt-in** checks;
they are not part of the offline suite. Inspect a tool's parameters before using
it. A refactor can be verified without starting battles.
