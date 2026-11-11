# Run from the authoring workspace. Keeps each isolated Unity project and build log for inspection.
param([string]$Output = '', [string]$WorkRoot = '')
$ErrorActionPreference = 'Stop'
$pipeline = Split-Path -Parent $MyInvocation.MyCommand.Path
$mod = Split-Path -Parent $pipeline
$unity = if ($env:UNITY_2020_EXE) { $env:UNITY_2020_EXE } else { 'D:\Unity\Editor\Unity.exe' }
if (-not (Test-Path -LiteralPath $unity)) { throw 'Unity 2020.3.1f1 is required; set UNITY_2020_EXE.' }
$source = Join-Path $pipeline 'unity\Assets\MajinFlesh'
foreach ($name in @('flesh-albedo.png', 'flesh-normal.png', 'MajinFlesh.shader')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name))) { throw "Missing authored flesh asset: $name" }
}
if (-not $WorkRoot) { $WorkRoot = Join-Path (Get-Location).Path 'build' }
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
$unique = 'majinflesh-unity-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$project = Join-Path $WorkRoot $unique
New-Item -ItemType Directory -Force -Path (Join-Path $project 'ProjectSettings'), (Join-Path $project 'Packages'), (Join-Path $project 'Assets\Editor'), (Join-Path $project 'Assets\MajinFlesh') | Out-Null
foreach ($name in @('flesh-albedo.png', 'flesh-normal.png', 'MajinFlesh.shader')) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $project 'Assets\MajinFlesh')
}
Copy-Item -LiteralPath (Join-Path $pipeline 'unity\Assets\Editor\MajinFleshBuild.cs') -Destination (Join-Path $project 'Assets\Editor')
Set-Content -LiteralPath (Join-Path $project 'Packages\manifest.json') -Value '{ "dependencies": { "com.unity.modules.assetbundle": "1.0.0", "com.unity.modules.imageconversion": "1.0.0" } }' -Encoding ascii
Set-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Value "m_EditorVersion: 2020.3.1f1`nm_EditorVersionWithRevision: 2020.3.1f1 (77a89f25062f)" -Encoding ascii
if (-not $Output) { $Output = Join-Path $mod 'Assets\VFX\MajinFlesh' }
$Output = [IO.Path]::GetFullPath($Output)
$log = Join-Path $project 'build.log'
$arguments = @('-batchmode', '-quit', '-force-d3d11', '-projectPath', $project, '-executeMethod', 'MajinFleshBuild.CommandLine', '-majinFleshOut', $Output, '-logFile', $log)
$quotedArguments = $arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
Write-Host ('Unity project: ' + $project)
Write-Host ('Unity log: ' + $log)
$process = Start-Process -FilePath $unity -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
$null = $process.Handle
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    Select-String -LiteralPath $log -Pattern 'Shader error|error CS|Exception|failed|line \d+:' | Select-Object -Last 30 | ForEach-Object { $_.Line }
    throw "Unity Majin flesh build failed ($($process.ExitCode)); retained project/log: $project"
}
if (-not (Test-Path -LiteralPath (Join-Path $Output 'majinflesh'))) { throw "Unity did not produce the flesh bundle; see $log" }
Write-Host ('OK: ' + (Join-Path $Output 'majinflesh'))
