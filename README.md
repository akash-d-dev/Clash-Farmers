# Clash Farmer

Windows app for BlueStacks or Google Play Games. Uses .NET Framework, Windows PowerShell 5.1 and Windows English OCR. No extra packages are required.

## Project layout

```text
Clash Farmer/
  Start.cmd              Open the app (PowerShell fallback if not built)
  Start.ps1              PowerShell entry point, including -SelfTest
  Build.ps1              Build entry point
  src/                   C# application and executable host
  scripts/               Build, startup, OCR, and shared path helpers
  assets/                Runtime card references and placement example
  config/                Shareable settings.example.json
  tests/                 Offline checks and fixtures/
  tools/                 Manual inspection and explicitly enabled live checks
  bin/                   Generated executables, ignored by Git
  data/                  Local settings, logs, diagnostics, captures, and previews
```

On first launch, `config/settings.example.json` is copied to `data/settings.json`. Existing local settings are preserved. Build outputs and the entire `data/` directory stay out of Git; they are generated or personal run data. Screenshots required by offline checks live in `tests/fixtures/`, and checks write only to `data/test-results/` or an isolated temporary directory. Scripts resolve paths relative to the project, so the folder can be moved and can contain spaces.

## Start

1. Open Clash of Clans on the home village with menus closed and its normal centered camera.
2. Open Start.cmd, select the platform, and set the controls below. A fresh clone can run directly through Windows PowerShell; optionally run Build.ps1 first to create the executable.
3. Click Start. A three-second countdown lets the runner bring the game forward.

| Control | Range / behavior |
| --- | --- |
| Battles | 1–30 completed attacks |
| Troops per attack | 1–500 placement click attempts, always from the first troop card |
| End battle after (s) | 3–180 seconds after the final troop click, randomized by ±5 seconds and clamped to 3–180 |
| Gold / Elixir / Dark elixir min | 0–99,999,999 each; zero disables that resource check |

Settings save as you change them and are locked during a run. Existing gold/elixir defaults remain 500,000 each; dark elixir defaults to zero. Default troop count is 25 and default duration is 90 seconds. The old duration bounds migrate to their midpoint. Known old short deployment lines migrate to the wider outer-strip preset.

Gold and elixir arrows step by 100,000; dark elixir arrows step by 10,000. All three have a minimum of zero, and you can still type an exact value. Numeric inputs and loot logs use international comma grouping, such as `500,000` and `1,000,000`, regardless of the Windows number-grouping preference.

## Platforms and Stop

Google Play Games uses desktop screenshots and mouse input. The separate Clash of Clans window must stay visible and active; the Google launcher can be closed. Start maximizes the game, detects its rendered game area, excludes the sidebar/title bar and desktop gaps, and clips small custom-frame overlaps. Leave the mouse alone. Switching windows, covering the game, moving/resizing, or minimizing stops desktop control.

BlueStacks uses local ADB when enabled and otherwise uses desktop control. ADB does not require Windows focus or use the desktop mouse. Enable ADB yourself in BlueStacks Settings > Advanced, then leave Clash of Clans open. The runner accepts local emulator/loopback connections only and does not change security settings. See [BlueStacks setup](https://support.bluestacks.com/hc/en-us/articles/23925869130381-How-to-enable-Android-Debug-Bridge-on-BlueStacks-5).

Stop with F9, Esc, or the Stop button. Stop is enabled only while busy. Cancellation prevents further troop, spell, or exit clicks. Start begins a new run; internal recovery is available for a specifically restored counter/stage.

The window/taskbar title shows the activity first, followed by remaining battles and completed/total, for example `Searching | 20 left (10/30) - Clash Farmer`. The remaining count includes an attack in progress. Titles cover starting, home, searching, battle running, ending, returning home, recovery, stopped, and finished. Updates do not activate the app or interrupt the game. Windows must have taskbar labels enabled to show the full title directly; otherwise hover over the app's taskbar icon to read it.

## Deployment and spells

Google Play Games selects the first troop with the default `1` key and the first spell with `A`. It does not OCR troop-card counts or compare spell-card images for this platform. Keep the game's default key mappings enabled, load the intended troop in the first troop slot and Haste in the first spell slot, and keep the game active. Selection sends a short key press and release; Stop, focus/visibility, and held-key guards remain enforced. Loot and navigation still use screen recognition.

BlueStacks selects the leftmost troop stack using its stack marker and position rather than a P.E.K.K.A portrait, and checks the Haste reference image before clicking its saved spell-card position. A missing/unrecognized troop card stops before deployment. Both platforms use your configured troop click count without capping it to the actual inventory, switching cards, or changing the eleven-spell sequence.

Troops split evenly between the two sides of a randomly selected L; odd counts put the extra troop on a random side. Each side has evenly spaced slots, with jitter of ±20% of the average spacing. Fixed paths follow the outer grass strip and cover the visible side above the troop bar. Geometry version 4 adds a 48-pixel horizontal margin beyond the former lower-right tile-edge path in the normalized 1600×900 view. The other three paths and spell locations remain unchanged. Known older presets migrate automatically while preserving custom paths and run preferences. The upper side deploys first, allowing scouting-only boost buttons to disappear before lower-side placements.

Eleven Haste spells remain supported: three along each inward entry path, then a 5–10 second pause followed by five central casts at 2–5 second intervals. The opening six are scheduled 100 ms apart without OCR between casts. Opening targets aim for 120-pixel separation and central targets for 90 pixels in the normalized 1600×900 view. Bounded sampling chooses the best separation if the available area cannot meet those targets. Some effect-circle overlap can remain. Recognition and input overhead add to scheduled intervals.

A short attack duration cancels pending spells. A natural result ends deployment/casting early. The runner does not promise every requested troop click or spell will be sent if the game has already ended.

## Loot and timed exit

Only positive resource thresholds are read and checked. All enabled thresholds must pass; exact threshold values qualify. With all three set to zero, loot OCR is bypassed and the first verified opponent is attacked.

Loot readings must agree across image sizes and repeat consistently before comparison. Below-target bases get Next and do not count as completed battles. After three unreadable attempts, the current base is attacked with unverified loot, as requested. The failure counter resets for the next opponent. The log explicitly distinguishes accepted loot, below-target skips, and unreadable-loot attacks. The reader uses available loot on the left, not your storage on the right.

At the sampled deadline, the runner stops pending spell actions, recognizes either Surrender or End Battle, clicks it, then recognizes the matching dialog and clicks Okay. Both button types can require confirmation. Natural results take priority. Unknown dialogs are not accepted. Results are counted only after returning home. Stop remains available throughout.

## Placement and troubleshooting

Show troop positions draws the two L formations on a screenshot without sending game clicks. Cyan is the left L and magenta the right L. It is an approximate placement preview, not boundary detection. Use it with an opponent visible; the default camera/zoom and a 16:9 game area are required. Custom zooms, unusually cropped layouts, and scenery may require endpoint adjustment. The runner makes click attempts and cannot guarantee successful deployment of each troop or a triple.

Activity is saved to `data/run.log`. Failed button and loot crops are saved under `data/diagnostics/`. `data/google-viewport.txt` records Google's latest detected game bounds. Optional settings in `data/settings.json` include Normal/Reverse endpoint arrays, platform/control overrides, spell settings, and deployment delay bounds. Advanced custom geometry is preserved when it differs from the known old presets. Previous local settings backups, previews, and captures are retained under `data/backups/`, `data/previews/`, and `data/captures/`.

## Build and verification

Start.cmd opens `bin/ClashFarmer.exe`, falling back to `scripts/Start.ps1` when no executable exists. The build also generates `bin/ClashPekkas.exe` for filename compatibility. Rebuild while the runner is closed:

```powershell
powershell.exe -NoProfile -File .\Build.ps1
```

Run all offline checks (Windows English OCR must be available):

```powershell
powershell.exe -NoProfile -File .\tests\Test-All.ps1
```

Individual checks can be run from `tests/`, or use `Start.ps1 -SelfTest` for the fast logic suite. They send no live game input. Coverage includes exact variable troop clicks, balanced spacing, duration bounds and spell cancellation, natural completion, both exit confirmations, three resource thresholds, unreadable-loot fallback, Stop, settings persistence, first-card recognition, platform isolation, and the 30-battle cap. Screenshot fixtures include the supplied Surrender button and End Battle/Okay dialog. Generated geometry and UI images are saved to `data/test-results/`.

The scripts in `tools/` are manual utilities. `Run-OneBattle.ps1 -RunOneBattle` performs a live ADB attack and `Verify-Startup.ps1 -Navigate` performs live menu navigation; neither is included in Test-All.ps1.

To share the app, copy `Start.cmd`, `scripts/`, `src/`, `assets/`, `config/`, and optionally the built `bin/` directory together, keeping this layout. The recipient gets fresh defaults unless you deliberately include `data/settings.json`. After moving or renaming the project, update any desktop shortcuts to the new `Start.cmd` path.
