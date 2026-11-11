$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$config = if ($args.Count -gt 0 -and $args[0]) { $args[0] } else { "Debug" }

$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
if ($msbuild) {
  & $msbuild.Path ".\\DBPU.sln" "/t:Build" "/p:Configuration=$config" "/p:Platform=AnyCPU"
  if ($LASTEXITCODE -ne 0) { throw "msbuild failed with exit code $LASTEXITCODE" }
} else {
  & dotnet build ".\\DBPU.sln" "-c" $config
  if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
}

$built = Join-Path $root ("bin\\{0}\\DragonBallSingularity.dll" -f $config)
if (-not (Test-Path $built)) {
  throw "Build succeeded but output not found: $built"
}

$dest = Join-Path $root "dragon ball.dll"
Copy-Item -LiteralPath $built -Destination $dest -Force

$legacyPayload = Join-Path $root "DragonBallSingularity.dll"
if (Test-Path -LiteralPath $legacyPayload) {
  Remove-Item -LiteralPath $legacyPayload -Force
}

$shippedAssetBundle = Join-Path $root "UnityEngine.AssetBundleModule.dll"
if (Test-Path -LiteralPath $shippedAssetBundle) {
  Remove-Item -LiteralPath $shippedAssetBundle -Force
}

foreach ($folder in @("bin", "obj")) {
  $path = Join-Path $root $folder
  if (Test-Path -LiteralPath $path) {
    Remove-Item -LiteralPath $path -Recurse -Force
  }
}

Write-Host ("OK: " + $dest)
