param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\RopedTogether\RopedTogether.csproj"

& dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $root "src\RopedTogether\bin\$Configuration\RopedTogether.dll"
$outDir = Join-Path $root "artifacts\RopedTogether"
$zip = Join-Path $root "artifacts\RopedTogether-1.0.0.zip"

Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $outDir -ItemType Directory -Force | Out-Null
Copy-Item $dll $outDir
Copy-Item (Join-Path $root "README.md") $outDir
Copy-Item (Join-Path $root "CHANGELOG.md") $outDir
Copy-Item (Join-Path $root "manifest.json") $outDir

Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zip
Write-Host "Created $zip"
