param(
    [string]$Adb = 'E:\Unity\Hub\Editor\2021.3.45f2\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
    [string]$Serial = '',
    [string]$Apk = 'E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk',
    [int]$Runs = 3
)

$ErrorActionPreference = 'Stop'
$package = 'com.breakline.m1'
$activity = 'com.unity3d.player.UnityPlayerActivity'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outDir = Join-Path 'E:\小游戏开发\docs\evidence' "cloud-$stamp"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$adbArgs = @()
if (-not [string]::IsNullOrWhiteSpace($Serial)) { $adbArgs += @('-s', $Serial) }

Write-Host "ADB: $Adb"
Write-Host "APK: $Apk"
& $Adb @adbArgs devices -l
if (-not (Test-Path $Apk)) { throw "APK not found: $Apk" }

$deviceInfo = @(
    ('model=' + (& $Adb @adbArgs shell getprop ro.product.model)),
    ('soc=' + (& $Adb @adbArgs shell getprop ro.soc.model)),
    ('android=' + (& $Adb @adbArgs shell getprop ro.build.version.release)),
    ('sdk=' + (& $Adb @adbArgs shell getprop ro.build.version.sdk)),
    ('size=' + (& $Adb @adbArgs shell wm size)),
    ('density=' + (& $Adb @adbArgs shell wm density))
)
$deviceInfo | Set-Content (Join-Path $outDir 'device-info.txt') -Encoding UTF8

& $Adb @adbArgs install -r $Apk
if ($LASTEXITCODE -ne 0) { throw "adb install failed" }

$launch = {
    param([string]$mode)
    & $Adb @adbArgs shell am force-stop $package
    Start-Sleep -Seconds 2
    & $Adb @adbArgs shell am start -n "$package/$activity" --es breakline_auto $mode
}

function Pull-RunFiles([string]$label) {
    $remote = "/sdcard/Android/data/$package/files/"
    & $Adb @adbArgs pull $remote (Join-Path $outDir $label)
    $shot = "/sdcard/breakline-$label-screen.png"
    & $Adb @adbArgs shell screencap -p $shot
    & $Adb @adbArgs pull $shot (Join-Path $outDir "$label-screen.png")
}

# P4: 200 visible + 12 stress physical + chain, three cold runs.
for ($i = 1; $i -le $Runs; $i++) {
    $label = "p4-run$i"
    Write-Host "=== $label ==="
    & $launch 'stress'
    Start-Sleep -Seconds 16
    Pull-RunFiles $label
    Start-Sleep -Seconds 5
}

# P5: 200 full Rigidbody2D, non-gating.
for ($i = 1; $i -le $Runs; $i++) {
    $label = "p5-run$i"
    Write-Host "=== $label ==="
    & $launch 'full'
    Start-Sleep -Seconds 16
    Pull-RunFiles $label
    Start-Sleep -Seconds 5
}

# Crush/gate autotest: the app quits itself after capturing.
Write-Host '=== crush ==='
& $launch 'crush'
Start-Sleep -Seconds 8
Pull-RunFiles 'crush'

# Pull the aggregate summary and all raw frame files once more.
& $Adb @adbArgs pull "/sdcard/Android/data/$package/files/M1-Performance.csv" (Join-Path $outDir 'M1-Performance.csv')

Write-Host "Done. Evidence: $outDir"
Write-Host 'IMPORTANT: Do not use emulator/PC data for the M1 Go/No-Go gate.'
Write-Host 'Device / resolution / Android version must be recorded in docs/05_M1性能报告.md.'
