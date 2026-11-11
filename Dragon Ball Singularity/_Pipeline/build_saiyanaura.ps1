# Rebuilds Assets/VFX/SaiyanAura/saiyan_aura (the SSJ1/2/3 auras) from source. Needs Unity 2020.3.1f1, the game's version.
# Run saiyan_flipbook.py first when the reference animations change: it writes the atlases this packs.
# The Unity project is created in TEMP and removed afterwards, so nothing heavy stays in the mod.
#   ./build_saiyanaura.ps1 [-Output <folder>]
#   ./build_saiyanaura.ps1 -Preview <folder> [-Frames <n>] [-Work <folder>]   renders PNG frames with SaiyanAuraRig instead;
#                                                                            -Work keeps the project between runs.
param([string]$Output = '', [string]$Preview = '', [int]$Frames = 24, [string]$Work = '')
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$mod = Split-Path -Parent $here
$unity = if ($env:UNITY_2020_EXE) { $env:UNITY_2020_EXE } else { 'D:\Unity\Editor\Unity.exe' }
if (-not (Test-Path -LiteralPath $unity)) { throw 'Unity 2020.3.1f1 was not found (set UNITY_2020_EXE).' }
$keep = [bool]$Work
if (-not $Work) { $Work = Join-Path $env:TEMP 'DBPU-SaiyanAura-Unity' }
if (-not $keep -and (Test-Path -LiteralPath $Work)) { Remove-Item -LiteralPath $Work -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $Work 'ProjectSettings'), (Join-Path $Work 'Assets\Editor'), (Join-Path $Work 'Assets\SaiyanAura'), (Join-Path $Work 'Packages') | Out-Null
# A kept project may hold atlases an older pipeline wrote; the folder is rebuilt from the sources every run.
Get-ChildItem -LiteralPath (Join-Path $Work 'Assets\SaiyanAura') -File | Where-Object { $_.Extension -in '.png', '.bytes' } | Remove-Item -Force
Copy-Item -Path (Join-Path $here 'unity\Assets\SaiyanAura\*.shader'), (Join-Path $here 'unity\Assets\SaiyanAura\*.cginc'), (Join-Path $here 'unity\Assets\SaiyanAura\*.png'), (Join-Path $here 'unity\Assets\SaiyanAura\*.bytes') -Destination (Join-Path $Work 'Assets\SaiyanAura') -Force
Copy-Item -LiteralPath (Join-Path $here 'unity\Assets\Editor\SaiyanAuraBuild.cs') -Destination (Join-Path $Work 'Assets\Editor') -Force
Set-Content -LiteralPath (Join-Path $Work 'Packages\manifest.json') -Value '{ "dependencies": { "com.unity.modules.assetbundle": "1.0.0", "com.unity.modules.imageconversion": "1.0.0", "com.unity.modules.particlesystem": "1.0.0" } }' -Encoding ascii
Set-Content -LiteralPath (Join-Path $Work 'ProjectSettings\ProjectVersion.txt') -Value "m_EditorVersion: 2020.3.1f1`nm_EditorVersionWithRevision: 2020.3.1f1 (77a89f25062f)" -Encoding ascii
$log = Join-Path $Work 'build.log'
if ($Preview)
{
    Copy-Item -LiteralPath (Join-Path $here 'unity\PreviewEditor\SaiyanAuraPreview.cs') -Destination (Join-Path $Work 'Assets\Editor') -Force
    # A MonoBehaviour cannot be added from an Editor folder.
    New-Item -ItemType Directory -Force -Path (Join-Path $Work 'Assets\PreviewRuntime') | Out-Null
    Copy-Item -LiteralPath (Join-Path $mod 'Assets\scripts\Auras\SaiyanAuraRig.cs'), (Join-Path $mod 'Assets\scripts\Auras\SaiyanAuraFrames.cs') -Destination (Join-Path $Work 'Assets\PreviewRuntime') -Force
    $arguments = @('-batchmode', '-quit', '-projectPath', $Work, '-executeMethod', 'SaiyanAuraPreview.CommandLine', '-previewOut', $Preview, '-previewFrames', "$Frames", '-logFile', $log)
}
else
{
    # A kept project may hold the preview scripts from an earlier run.
    Remove-Item -LiteralPath (Join-Path $Work 'Assets\Editor\SaiyanAuraPreview.cs'), (Join-Path $Work 'Assets\PreviewRuntime') -Recurse -Force -ErrorAction SilentlyContinue
    if (-not $Output) { $Output = Join-Path $mod 'Assets\VFX\SaiyanAura' }
    $arguments = @('-batchmode', '-quit', '-projectPath', $Work, '-executeMethod', 'SaiyanAuraBuild.CommandLine', '-saiyanAuraOut', $Output, '-logFile', $log)
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
if ($Preview) { Write-Host ('OK: ' + $Preview) } else { Write-Host ('OK: ' + (Join-Path $Output 'saiyan_aura')) }
