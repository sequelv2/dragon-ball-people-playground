# Rebuilds Assets/VFX/SparkingOutline/sparking-outline from source. Needs Unity 2020.3.1f1, the game's version.
# The Unity project is created in TEMP and removed afterwards, so nothing heavy stays in the mod.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$mod = Split-Path -Parent $here
$unity = if ($env:UNITY_2020_EXE) { $env:UNITY_2020_EXE } else { 'D:\Unity\Editor\Unity.exe' }
if (-not (Test-Path -LiteralPath $unity)) { throw 'Unity 2020.3.1f1 was not found (set UNITY_2020_EXE).' }
$work = Join-Path $env:TEMP 'DBPU-SparkingOutline-Unity'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $work 'ProjectSettings'), (Join-Path $work 'Assets\Editor'), (Join-Path $work 'Assets\SparkingOutline'), (Join-Path $work 'Packages') | Out-Null
Copy-Item -Path (Join-Path $here 'unity\Assets\SparkingOutline\*.shader') -Destination (Join-Path $work 'Assets\SparkingOutline') -Force
Copy-Item -LiteralPath (Join-Path $here 'unity\Assets\Editor\SparkingOutlineBuild.cs') -Destination (Join-Path $work 'Assets\Editor') -Force
Set-Content -LiteralPath (Join-Path $work 'Packages\manifest.json') -Value '{ "dependencies": { "com.unity.modules.assetbundle": "1.0.0" } }' -Encoding ascii
Set-Content -LiteralPath (Join-Path $work 'ProjectSettings\ProjectVersion.txt') -Value "m_EditorVersion: 2020.3.1f1`nm_EditorVersionWithRevision: 2020.3.1f1 (77a89f25062f)" -Encoding ascii
$output = Join-Path $mod 'Assets\VFX\SparkingOutline'
$log = Join-Path $work 'build.log'
$arguments = @('-batchmode', '-quit', '-projectPath', $work, '-executeMethod', 'SparkingOutlineBuild.CommandLine', '-sparkingOutlineOut', $output, '-logFile', $log)
$process = Start-Process -FilePath $unity -ArgumentList ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -PassThru
$null = $process.Handle
$process.WaitForExit()
if ($process.ExitCode -ne 0)
{
    Select-String -LiteralPath $log -Pattern 'Shader error|error CS|line \d+:|Exception' | Select-Object -First 20 | ForEach-Object { $_.Line }
    throw "Unity build failed ($($process.ExitCode)); log kept at $log"
}
Remove-Item -LiteralPath $work -Recurse -Force
Write-Host ('OK: ' + (Join-Path $output 'sparking-outline'))
