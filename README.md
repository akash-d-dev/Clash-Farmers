# Clash Farmer

Windows app for BlueStacks or Google Play Games Developer Emulator. Uses .NET Framework, Windows PowerShell 5.1 and Windows English OCR. No extra packages are required.

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
| Deployment | L-shaped (two adjacent sides) or All four sides; defaults to L-shaped |
| Spells per attack | 0–100 Haste cast attempts from the selected first spell; zero skips spells |
| Spell depth (%) | 0–100; 0 is the troop deployment line and 100 is the estimated base center. Arrows step by 5; exact percentages can be typed. |
| Deploy siege + all heroes / abilities | Google Developer mode only; enabled by default. Heroes first, then siege, then army, then hero ability keys. |
| End battle after (s) | 3–180 seconds from the first troop placement, randomized by ±5 seconds and clamped to 3–180 |
| Gold / Elixir / Dark elixir min | 0–99,999,999 each; zero disables that resource check |

Settings save as you change them and are locked during a run. Existing gold/elixir defaults remain 500,000 each; dark elixir defaults to zero. Default troop count is 25 and default duration is 90 seconds. The old duration bounds migrate to their midpoint. Known old short deployment lines migrate to the wider outer-strip preset.

Gold and elixir arrows step by 100,000; dark elixir arrows step by 10,000. All three have a minimum of zero, and you can still type an exact value. Numeric inputs and loot logs use international comma grouping, such as `500,000` and `1,000,000`, regardless of the Windows number-grouping preference.

## Platforms and Stop

Google Play Developer (ADB) replaces the normal Google Play Games desktop mode. Open Clash of Clans in the Developer Emulator at the village, then select this platform in Clash Farmer. Screenshots, taps and selection keys go through its bundled ADB, normally at `localhost:6520` (the equivalent `127.0.0.1:6520` is accepted). The emulator can remain minimized or covered while you use other Windows apps and the desktop mouse. Keep the emulator running and Clash of Clans open inside Android. Windows focus, window position and window size are irrelevant; the Android display must remain 16:9. Existing `Platform=GooglePlayGames` settings automatically use this new mode. There is no fallback to desktop input if ADB is unavailable.

The bundled executable is normally `C:\Program Files\Google\Play Games Developer Emulator\current\emulator\adb.exe`. Optional `GoogleAdbPath` and `GoogleAdbSerial` settings are separate from BlueStacks' overrides. The connection accepts only local loopback endpoints, reuses an already-online alias and reconnects stale offline aliases only at startup. Connection failures stop the run; taps are not blindly retried. See [Google's Developer Emulator documentation](https://developer.android.com/games/playgames/pg-emulator).

BlueStacks uses local ADB when enabled and otherwise uses desktop control. ADB does not require Windows focus or use the desktop mouse. Enable ADB yourself in BlueStacks Settings > Advanced, then leave Clash of Clans open. The runner accepts local emulator/loopback connections only and does not change security settings. See [BlueStacks setup](https://support.bluestacks.com/hc/en-us/articles/23925869130381-How-to-enable-Android-Debug-Bridge-on-BlueStacks-5).

Stop with F9, Esc, or the Stop button. F9/Esc are global, including while working in another app. Stop is enabled only while busy. Cancellation prevents further troop, spell, or exit clicks after any current ADB command returns. Start begins a new run; internal recovery is available for a specifically restored counter/stage.

The window/taskbar title shows the activity first, followed by remaining battles and completed/total, for example `Searching | 20 left (10/30) - Clash Farmer`. The remaining count includes an attack in progress. Titles cover starting, home, searching, battle running, ending, returning home, recovery, stopped, and finished. Updates do not activate the app or interrupt the game. Windows must have taskbar labels enabled to show the full title directly; otherwise hover over the app's taskbar icon to read it.

## Deployment and spells

Google Developer mode selects the first troop with Android key event `1` and the first spell with `A`, sent through ADB rather than Windows input. Keep default game mappings enabled, the intended troop in the first troop slot, and the intended spell in the first spell slot. Troop/spell selection does not use card OCR or reference images. Loot and navigation still use OCR on ADB screenshots. The game must be the foreground app inside Android, even though the Windows emulator window can remain minimized.

With support enabled, the runner directly attempts heroes `Q/W/E/R`, then siege `Z` on the first side, then the configured army from troop `1`. In four-side mode each hero has one side; in L-shaped mode Q/W share the first side and E/R share the second. Each hero is placed at that side's troop-line midpoint. After the last troop placement settles for 350 ms, the runner sends the queued Q/W/E/R ability keys, then casts spells. Abilities still run when spells are disabled. These are input attempts, not confirmed deployments or abilities. At the user's request, unavailable cards are attempted without the former image-recognition gate; load the expected army/card layout before starting. The saved toggle retains its legacy JSON name `DeploySiegeAndFirstHero` for compatibility. BlueStacks retains its existing troop/spell flow.

BlueStacks selects the leftmost troop stack using its stack marker and position rather than a P.E.K.K.A portrait, and checks the Haste reference image before clicking its saved spell-card position. A missing/unrecognized troop card stops before deployment. Both platforms use your configured troop and spell click counts without capping them to actual inventory or switching cards. The existing advanced `UseSpells=false` setting still disables spells regardless of the configured count.

Troops split evenly between the two sides of a randomly selected L, or all four sides according to Deployment. Remainder troops go to distinct randomly chosen sides, so side counts differ by at most one. Each side has evenly spaced slots, with jitter of ±20% of the average spacing. Fixed paths follow the outer grass strip and cover the visible side above the troop bar. Geometry version 4 adds a 48-pixel horizontal margin beyond the former lower-right tile-edge path in the normalized 1600×900 view. Known older presets migrate automatically while preserving custom paths and run preferences. Upper sides deploy first, allowing scouting-only boost buttons to disappear before lower-side placements.

Spells per attack defaults to eleven. In L-shaped mode, counts of 1–6 are all spread across the two selected entry paths and cast immediately at the opening depth, with no later batch. Above six, the rounded-up half is cast immediately; the remaining half follows a 5–10 second pause at 2–5 second intervals. Seven means four opening and three delayed spells; eleven means six opening and five delayed spells. In All four sides mode, all configured spells are divided across the four entry paths and cast quickly, with no delayed group; eleven means 3/3/3/2 in random remainder order. Zero skips spell selection entirely. Opening casts are scheduled 100 ms apart, with a result check after each six casts during longer bursts. Recognition and input overhead add to scheduled intervals.

Troop input waits are measured after input completion, including any screen-reading time earlier in the tick. The runner waits 500 ms after troop selection and the first placement to let selection and the scouting-to-battle transition settle, then uses the configured placement intervals. It waits 350 ms after the final troop release before abilities and spells, and checks desktop mouse-release failures. Counts remain exact click attempts: the runner does not add blind extra clicks or infer accepted deployments from the log.

L-shaped mode shows separate Opening and Later spell depth controls, defaulting to 0% and 70%. All four sides shows one Spell depth control, defaulting to 0%. Each value is saved independently across mode switches. Controls lock while running and disable when spells are off or zero; Later depth also disables when there is no later batch. Depth is a percentage of the distance from the actual saved troop path toward the estimated center, not a tile count. Along-edge spacing varies slightly, but depth does not. Opening targets aim for 120-pixel separation and later targets for 90 pixels in the normalized 1600×900 view; bounded sampling picks the best available spacing. At 100%, all positions converge at the center and overlap is expected. Invalid custom paths that place spells on the HUD are rejected rather than silently changing depth.

Existing settings receive `LShapeOpeningDepthPercent=0`, `LShapeLaterDepthPercent=70`, and `FourSidesSpellDepthPercent=0` when absent, while retaining custom troop paths and other preferences. The older `OpeningSpellInward`, `NormalSpellGuide`, and `ReverseSpellGuide` fields are retained for JSON compatibility but no longer affect spell placement or validation.

A short attack duration cancels pending spells. A natural result ends deployment/casting early. The runner does not promise every requested troop click or spell will be sent if the game has already ended.

## Loot and timed exit

Only positive resource thresholds are read and checked. All enabled thresholds must pass; exact threshold values qualify. With all three set to zero, loot OCR is bypassed and the first verified opponent is attacked.

Loot readings must agree across image sizes and repeat consistently before comparison. Below-target bases get Next and do not count as completed battles. After three unreadable attempts, the current base is attacked with unverified loot, as requested. The failure counter resets for the next opponent. The log explicitly distinguishes accepted loot, below-target skips, and unreadable-loot attacks. The reader uses available loot on the left, not your storage on the right.

The cutoff is sampled once when the first army troop placement is sent; hero and siege setup time does not count and the timer is not restarted when the army finishes. At the deadline, the runner stops remaining troop placements, queued abilities and spells, recognizes either Surrender or End Battle, clicks it, then recognizes the matching dialog and clicks Okay. A short cutoff can therefore end before the full army is deployed. Both button types can require confirmation. Natural results take priority. Unknown dialogs are not accepted. Results are counted only after returning home. Stop remains available throughout.

## Placement and troubleshooting

Show placement preview draws the selected deployment mode, per-side troop counts, and spell targets on a screenshot without sending game clicks. In L-shaped mode, cyan troops show the left L and magenta troops the right L; in All four sides mode, cyan shows all four paths. Yellow circles mark opening spells and orange squares mark later spells; the legend includes configured depths. Spells are omitted when disabled. It is an approximate placement preview, not boundary detection. Use it with an opponent visible; the default camera/zoom and a 16:9 game area are required. Custom zooms, unusually cropped layouts, and scenery may require endpoint adjustment. The runner makes click attempts and cannot guarantee successful deployment of each troop or a triple.

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
