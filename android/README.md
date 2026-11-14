# Android zoom helper

`ZoomInput.java` sends one centered two-finger pinch through Android's input
service using the existing ADB shell identity. A downward wheel event was ignored
by the tested game, and Android 14's `input` command lacks a scroll command.
The pinch was verified on the Google Play Developer emulator by zooming the home
village in and back out. No battle is required for that verification.

The app bundles the compiled `assets/android-zoom.dex` (about 5 KB). Java and D8
are **build-time tools only**. Normal C# builds use that bundled file and perform
no downloads. The helper is not an APK, does not run continuously, and changes no
Android settings or permissions. Reflection accesses the same input service
used by Android's shell input tools; future Android versions can require updates.

`AdbClient` derives a remote filename under `/data/local/tmp/` from the local
SHA-256 checksum, verifies the remote checksum, and sets the file read-only before
execution. Subsequent gestures reuse the verified file. Each process handles one
320 ms pinch, releases both contacts, prints `ZOOM_OUT_SENT`, and exits. A failed
gesture attempts ACTION_CANCEL. The runner checks the scene before every gesture
and checks Stop between gestures. The acknowledgment confirms input injection,
not a measured zoom level; three large inward pinches aim to reach the game's
zoom-out limit. Windows display scaling does not affect the gesture coordinates.

## Rebuilding after Java source changes

Use a JDK 17 and Google's D8 compiler (R8 JAR), then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Build-AndroidZoom.ps1 `
  -JdkDirectory 'C:\path\to\jdk-17' -D8Jar 'C:\path\to\r8.jar'
```

The script compiles Java 8 bytecode, converts it to DEX with minimum API 26, and
updates the bundled asset. Intermediate files stay in ignored
`data/test-results/android-zoom-build-*/`. Run `tests/Test-All.ps1` and rebuild the
C# app afterward. Commit the Java source and the DEX together.

The checked-in helper was built with Microsoft OpenJDK 17.0.17+10 and R8 8.3.37.
Toolchain sources: [Microsoft OpenJDK](https://learn.microsoft.com/en-us/java/openjdk/download)
and [Google R8/D8](https://r8.googlesource.com/r8/).

For deliberate home-only verification, the helper accepts an optional final
`in` argument to reverse the pinch. The runner never uses that argument. Do not
run it while an automation is active or a troop/spell is selected. Normal offline
tests use fake ADB and fake games; they do not execute the helper on a device.
