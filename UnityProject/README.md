# BREAKLINE M1 Unity Project

M1 greybox source for the approved 1-character / 1-destructible-test-level milestone.
Only M1 content is present. Do not add M2 characters, levels, skins, accounts, or multiplayer here.

## Status

- Runtime source is present under `Assets/Breakline/Runtime`.
- Editor build script is under `Assets/Breakline/Editor`.
- Tuning CSV files are under `Assets/Resources/Config`.
- The project has **not yet been imported/compiled by Unity** on this machine because the Unity editor download endpoint currently returns 404 and no Unity license is activated.
- No APK or real-device performance data has been produced yet. Do not treat this tree as a verified build.
- M1 greybox uses the **built-in render pipeline + Sprites/Default + a custom low-res RenderTexture**, not URP 2D. See `docs/03_开工状态与外部阻塞.md` §2.1; this is a disclosed deviation, not a silently completed URP target.

## Prerequisites (external)

1. Unity 2021.3.45f2 LTS editor installed with:
   - Android Build Support
   - Android SDK & NDK Tools
   - Android OpenJDK
2. Unity Personal license activated in Unity Hub.
3. Android platform-tools / `adb` available.
4. A physical Redmi Note 12 (or approved equivalent) at 1080p/60 Hz for the final gate.

## Build after prerequisites are met

From the project root, with the installed editor path:

```powershell
& 'E:\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\小游戏开发\UnityProject' `
  -executeMethod Breakline.EditorTools.BuildScript.BuildDebug `
  -logFile 'E:\小游戏开发\logs\build-debug.log' `
  -quit
```

Release:

```powershell
& 'E:\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\小游戏开发\UnityProject' `
  -executeMethod Breakline.EditorTools.BuildScript.BuildRelease `
  -logFile 'E:\小游戏开发\logs\build-release.log' `
  -quit
```

Outputs are written to `Builds/` under the project root.

Internal Windows visual check (does not replace Android verification):

```powershell
& 'E:\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\小游戏开发\UnityProject' `
  -executeMethod Breakline.EditorTools.BuildScript.BuildWindowsSmoke `
  -logFile 'E:\小游戏开发\logs\build-windows.log' `
  -quit
```

Then run `Builds/BREAKLINE_SMOKE.exe --smoke` for an idle screenshot, or
`Builds/BREAKLINE_SMOKE.exe --autotest` to teleport to C, trigger the barrel chain,
assert crush/gate state, capture a screenshot, and quit. Screenshots land in
`Application.persistentDataPath` (`m1-smoke.png` / `m1-autotest.png`).

Android automated test hook (M1 dev only, no effect on normal launch):

```powershell
# P4 auto-stress: 200 visible + 12 physical + chain, 10 s CSV, screenshot at ~7 s
adb shell am start -n com.breakline.m1/com.unity3d.player.UnityPlayerActivity `
  --es breakline_auto stress

# Crush autotest on device: teleport to C, trigger chain, assert crush/gate, screenshot, quit
adb shell am start -n com.breakline.m1/com.unity3d.player.UnityPlayerActivity `
  --es breakline_auto crush
```

Files are under `Application.persistentDataPath` (usually
`/sdcard/Android/data/com.breakline.m1/files`): `M1-Performance.csv`,
`M1-RawFrames-*.csv`, and `m1-stress.png`. Pull them with
`adb pull /sdcard/Android/data/com.breakline.m1/files/`.
This hook is test-only and does not add gameplay content.

## Controls

- Left joystick: move; direction also biases auto-aim.
- Jump: jump; hold Down/aim down and press Jump in the air for a ground slam.
- Fire: 3-round rifle burst; auto-aim locks visible targets. Third round pierces.
- Grenade: contact-fused throw; use it and barrels to reshape terrain.
- Context knife: fully automatic proximity reaction. The ring appears when an enemy is in knife range; a tick and a 2-frame hitstop confirm the trigger. It is suppressed whenever Fire/Grenade is held or newly pressed; it never consumes those inputs.

## Tuning

- Each CSV has `key,value,unit,min,max,tune_up,tune_down`.
- `tune_up` / `tune_down` explain what the player feels when increasing/decreasing the value.
- The runtime loads `Resources/Config/*.csv`.
- `docs/04_M1调参表说明.md` will hold the hand-off notes once compiled; the current source table itself is authoritative.
- A dev-only override can be placed at `Application.persistentDataPath/M1TuningOverride.csv` with `key,value` rows (loaded on next boot).

## Debug panel

- Tap `DBG` or press F1.
- `STRESS 200 + 12 + CHAIN`: 200 long-lived T2 fragments + 12 physical pieces + a chain; records 10s to `M1-Performance.csv`.
- `FULL 200 RIGIDBODIES`: P5-only experiment. This is **not** a Go/No-Go gate; it exists to report the M3 optimization headroom honestly.
- `DEBUG SLOWMO: BOSS/CLEAR`: validates the two whitelisted slow-motion events. Normal kills never use slow motion.
- `WARP TO B TUNNEL` / `WARP TO CRUSH ARENA`: quick access for feel testing.

## Known code-level risks before compile

- First Unity import may reveal API/版本 differences; the target version is locked in `ProjectSettings/ProjectVersion.txt`.
- No compiler check has been run yet.
- The build script intentionally forces legacy Input Manager because the project uses `UnityEngine.Input`.
- Debug UI is M1-only and must not ship in M3.
