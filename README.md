# Clash Farmer

Windows app for BlueStacks or Google Play Games. Uses .NET Framework, Windows PowerShell 5.1 and Windows English OCR. No extra packages are required.

## Start

1. Open Clash of Clans on the home village with menus closed and its normal centered camera.
2. Open Start.cmd, select the platform, and set the controls below.
3. Click Start. A three-second countdown lets the runner bring the game forward.

| Control | Range / behavior |
| --- | --- |
| Battles | 1–30 completed attacks |
| Troops per attack | 1–500 placement click attempts, always from the first troop card |
| End after troops (s) | 3–180 seconds after the final troop click, randomized by ±5 seconds and clamped to 3–180 |
| Gold / Elixir / Dark elixir min | 0–99,999,999 each; zero disables that resource check |

Settings save as you change them and are locked during a run. Existing gold/elixir defaults remain 500,000 each; dark elixir defaults to zero. Default troop count is 25 and default duration is 90 seconds. The old duration bounds migrate to their midpoint. Known old short deployment lines migrate to the wider outer-strip preset.

## Platforms and Stop

Google Play Games uses desktop screenshots and mouse input. The separate Clash of Clans window must stay visible and active; the Google launcher can be closed. Start maximizes the game, detects its rendered game area, excludes the sidebar/title bar and desktop gaps, and clips small custom-frame overlaps. Leave the mouse alone. Switching windows, covering the game, moving/resizing, or minimizing stops desktop control.

BlueStacks uses local ADB when enabled and otherwise uses desktop control. ADB does not require Windows focus or use the desktop mouse. Enable ADB yourself in BlueStacks Settings > Advanced, then leave Clash of Clans open. The runner accepts local emulator/loopback connections only and does not change security settings. See [BlueStacks setup](https://support.bluestacks.com/hc/en-us/articles/23925869130381-How-to-enable-Android-Debug-Bridge-on-BlueStacks-5).

Stop with F9, Esc, or the Stop button. Stop is enabled only while busy. Cancellation prevents further troop, spell, or exit clicks. Start begins a new run; internal recovery is available for a specifically restored counter/stage.

The window/taskbar title shows the activity first, followed by remaining battles and completed/total, for example `Searching | 20 left (10/30) - Clash Farmer`. The remaining count includes an attack in progress. Titles cover starting, home, searching, battle running, ending, returning home, recovery, stopped, and finished. Updates do not activate the app or interrupt the game. Windows must have taskbar labels enabled to show the full title directly; otherwise hover over the app's taskbar icon to read it.

## Deployment and spells

The runner selects the leftmost troop stack in the verified attack HUD, using its stack marker and position rather than a P.E.K.K.A portrait. It does not cap your requested clicks to the card inventory or switch cards when the stack runs out. Load the intended troop in the first slot and set its count yourself. A missing/unrecognized first troop card stops before deployment.

Troops split evenly between the two sides of a randomly selected L; odd counts put the extra troop on a random side. Each side has evenly spaced slots, with jitter of ±20% of the average spacing. Fixed paths follow the outer grass strip and cover the visible side above the troop bar. Geometry version 3 moves the upper paths away from buildings and gives the lower-right grass corridor its own coordinates; the working lower-left path remains unchanged. Existing version 2 presets migrate automatically without changing spell locations or run preferences. The upper side deploys first, allowing scouting-only boost buttons to disappear before lower-side placements.

Eleven Haste spells remain supported: three along each inward entry path, then a 5–10 second pause followed by five central casts at 2–5 second intervals. The opening six are scheduled 100 ms apart without OCR between casts. Opening targets aim for 120-pixel separation and central targets for 90 pixels in the normalized 1600×900 view. Bounded sampling chooses the best separation if the available area cannot meet those targets. Some effect-circle overlap can remain. Recognition and input overhead add to scheduled intervals.

A short attack duration cancels pending spells. A natural result ends deployment/casting early. The runner does not promise every requested troop click or spell will be sent if the game has already ended.

## Loot and timed exit

Only positive resource thresholds are read and checked. All enabled thresholds must pass; exact threshold values qualify. With all three set to zero, loot OCR is bypassed and the first verified opponent is attacked.

Loot readings must agree across image sizes and repeat consistently before comparison. Below-target bases get Next and do not count as completed battles. After three unreadable attempts, the current base is attacked with unverified loot, as requested. The failure counter resets for the next opponent. The log explicitly distinguishes accepted loot, below-target skips, and unreadable-loot attacks. The reader uses available loot on the left, not your storage on the right.

At the sampled deadline, the runner stops pending spell actions, recognizes either Surrender or End Battle, clicks it, then recognizes the matching dialog and clicks Okay. Both button types can require confirmation. Natural results take priority. Unknown dialogs are not accepted. Results are counted only after returning home. Stop remains available throughout.

## Placement and troubleshooting

Show troop positions draws the two L formations on a screenshot without sending game clicks. Cyan is the left L and magenta the right L. It is an approximate placement preview, not boundary detection. Use it with an opponent visible; the default camera/zoom and a 16:9 game area are required. Custom zooms, unusually cropped layouts, and scenery may require endpoint adjustment. The runner makes click attempts and cannot guarantee successful deployment of each troop or a triple.

Activity is saved to run.log. Failed button and loot crops are saved under diagnostics. google-viewport.txt records Google's latest detected game bounds. Optional settings include Normal/Reverse endpoint arrays, platform/control overrides, spell settings, and deployment delay bounds. Advanced custom geometry is preserved when it differs from the known old presets.

## Build and verification

Start.cmd opens ClashFarmer.exe. ClashPekkas.exe remains available for existing shortcuts. Rebuild while the runner is closed:

```powershell
powershell.exe -NoProfile -File .\Build.ps1
```

Automated checks send no live game input:

```powershell
powershell.exe -NoProfile -File .\Start.ps1 -SelfTest
powershell.exe -NoProfile -STA -File .\Test-Recognition.ps1
powershell.exe -NoProfile -STA -File .\Test-Navigation.ps1
powershell.exe -NoProfile -STA -File .\Test-Loot.ps1
powershell.exe -NoProfile -STA -File .\Test-GoogleLoot.ps1
powershell.exe -NoProfile -STA -File .\Test-Configurable.ps1
powershell.exe -NoProfile -STA -File .\Test-Adb.ps1
```

Coverage includes exact variable troop clicks, balanced spacing, duration bounds and spell cancellation, natural completion, both exit confirmations, three resource thresholds, unreadable-loot fallback, Stop, settings persistence, first-card recognition, platform isolation, and the 30-battle cap. Screenshot fixtures include the supplied Surrender button and End Battle/Okay dialog. outer-strip-preview.png shows the new geometry on the supplied edge-filled base. UI rendering is saved in app-preview.png.
