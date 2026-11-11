# Rebuilds Assets/VFX/BlueAura/saiyan_blue_aura (the Super Saiyan Blue, Blue Evolution and Kaioken over Blue
# auras drawn by Assets/scripts/Auras/BlueAuraRig.cs) from source. Needs Unity 2020.3.1f1, the game's version.
# The Unity project is created in TEMP and removed afterwards, so nothing heavy stays in the mod.
#   ./build_blueauras.ps1 [-Output <folder>]
#   ./build_blueauras.ps1 -Preview <folder> [-Work <folder>] [-Ids SSJBlue,KaiokenBlue] [-Frames 24] [-Rig <BlueAuraRig.cs>]
#       renders PNG sequences of the auras on a stand-in fighter instead of building; -Work keeps the project.
param([string]$Output = '', [string]$Preview = '', [string]$Work = '', [string]$Ids = 'SSJBlue,SSJBlueEvolution,KaiokenBlue', [int]$Frames = 24, [string]$Rig = '')
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$mod = Split-Path -Parent $here
$unity = if ($env:UNITY_2020_EXE) { $env:UNITY_2020_EXE } else { 'D:\Unity\Editor\Unity.exe' }
if (-not (Test-Path -LiteralPath $unity)) { throw 'Unity 2020.3.1f1 was not found (set UNITY_2020_EXE).' }
if (-not $Rig) { $Rig = Join-Path $mod 'Assets\scripts\Auras\BlueAuraRig.cs' }
$keep = [bool]$Work
if (-not $Work) { $Work = Join-Path $env:TEMP 'DBPU-BlueAura-Unity' }
if (-not $keep -and (Test-Path -LiteralPath $Work)) { Remove-Item -LiteralPath $Work -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $Work 'ProjectSettings'), (Join-Path $Work 'Assets\Editor'), (Join-Path $Work 'Assets\BlueAura'), (Join-Path $Work 'Packages') | Out-Null
Copy-Item -Path (Join-Path $here 'unity\Assets\BlueAura\*.shader'), (Join-Path $here 'unity\Assets\BlueAura\*.cginc') -Destination (Join-Path $Work 'Assets\BlueAura') -Force
Copy-Item -LiteralPath (Join-Path $here 'unity\Assets\Editor\BlueAuraBuild.cs') -Destination (Join-Path $Work 'Assets\Editor') -Force
Set-Content -LiteralPath (Join-Path $Work 'Packages\manifest.json') -Value '{ "dependencies": { "com.unity.modules.assetbundle": "1.0.0", "com.unity.modules.imageconversion": "1.0.0" } }' -Encoding ascii
Set-Content -LiteralPath (Join-Path $Work 'ProjectSettings\ProjectVersion.txt') -Value "m_EditorVersion: 2020.3.1f1`nm_EditorVersionWithRevision: 2020.3.1f1 (77a89f25062f)" -Encoding ascii
$log = Join-Path $Work 'build.log'
if ($Preview)
{
    Copy-Item -LiteralPath (Join-Path $here 'unity\PreviewEditor\BlueAuraPreview.cs') -Destination (Join-Path $Work 'Assets\Editor') -Force
    # A MonoBehaviour cannot be added from an Editor folder.
    New-Item -ItemType Directory -Force -Path (Join-Path $Work 'Assets\PreviewRuntime') | Out-Null
    Copy-Item -LiteralPath $Rig -Destination (Join-Path $Work 'Assets\PreviewRuntime\BlueAuraRig.cs') -Force
    $arguments = @('-batchmode', '-quit', '-projectPath', $Work, '-executeMethod', 'BlueAuraPreview.CommandLine', '-previewOut', $Preview, '-previewIds', $Ids, '-previewFrames', "$Frames", '-logFile', $log)
}
else
{
    # A kept project may hold the preview scripts from an earlier run.
    Remove-Item -LiteralPath (Join-Path $Work 'Assets\Editor\BlueAuraPreview.cs'), (Join-Path $Work 'Assets\PreviewRuntime') -Recurse -Force -ErrorAction SilentlyContinue
    if (-not $Output) { $Output = Join-Path $mod 'Assets\VFX\BlueAura' }
    $arguments = @('-batchmode', '-quit', '-projectPath', $Work, '-executeMethod', 'BlueAuraBuild.CommandLine', '-blueAuraOut', $Output, '-logFile', $log)
}
$process = Start-Process -FilePath $unity -ArgumentList ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -PassThru
$null = $process.Handle
$process.WaitForExit()
if ($process.ExitCode -ne 0)
{
    Select-String -LiteralPath $log -Pattern 'Shader error|error CS|line \d+:|Exception' | Select-Object -First 25 | ForEach-Object { $_.Line }
    throw "Unity build failed ($($process.ExitCode)); log kept at $log"
}
if (-not $keep) { Remove-Item -LiteralPath $Work -Recurse -Force }
if ($Preview) { Write-Host ('OK: ' + $Preview) } else { Write-Host ('OK: ' + (Join-Path $Output 'saiyan_blue_aura')) }
