# Rebuilds Assets/VFX/HitLens/hitlens from source. Needs Unity 2020.3.1f1, the game's version.
# The Unity project is created in TEMP and removed afterwards, so nothing heavy stays in the mod.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$mod = Split-Path -Parent $here
$unity = if ($env:UNITY_2020_EXE) { $env:UNITY_2020_EXE } else { 'D:\Unity\Editor\Unity.exe' }
if (-not (Test-Path -LiteralPath $unity)) { throw 'Unity 2020.3.1f1 was not found (set UNITY_2020_EXE).' }
$work = Join-Path $env:TEMP 'DBPU-HitLens-Unity'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $work 'ProjectSettings') | Out-Null
Copy-Item -LiteralPath (Join-Path $here 'unity\Assets') -Destination $work -Recurse
Copy-Item -LiteralPath (Join-Path $here 'unity\Packages') -Destination $work -Recurse
Set-Content -LiteralPath (Join-Path $work 'ProjectSettings\ProjectVersion.txt') -Value "m_EditorVersion: 2020.3.1f1`nm_EditorVersionWithRevision: 2020.3.1f1 (77a89f25062f)" -Encoding ascii
$output = Join-Path $mod 'Assets\VFX\HitLens'
$log = Join-Path $work 'build.log'
$arguments = @('-batchmode', '-quit', '-projectPath', $work, '-executeMethod', 'HitLensBuild.CommandLine', '-hitLensOut', $output, '-logFile', $log)
$process = Start-Process -FilePath $unity -ArgumentList ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -PassThru
$null = $process.Handle
$process.WaitForExit()
if ($process.ExitCode -ne 0)
{
    Select-String -LiteralPath $log -Pattern 'Shader error|error CS|line \d+:|Exception' | Select-Object -First 20 | ForEach-Object { $_.Line }
    throw "Unity build failed ($($process.ExitCode)); log kept at $log"
}
Remove-Item -LiteralPath $work -Recurse -Force
Write-Host ('OK: ' + (Join-Path $output 'hitlens'))
