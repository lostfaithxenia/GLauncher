param([string]$OutputDirectory = "")
$ErrorActionPreference = 'Stop'
$glauncherRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $glauncherRoot 'artifacts/GLauncher-0.1.0-alpha.1-win-x64' }
$glauncherDestination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $glauncherDestination) {
    if (Get-ChildItem -LiteralPath $glauncherDestination -Force | Select-Object -First 1) { throw 'Choose a new, empty output folder to preserve earlier builds.' }
}
& dotnet publish (Join-Path $glauncherRoot 'src/GLauncher.App/GLauncher.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -m:1 --disable-build-servers -o $glauncherDestination
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $glauncherRoot 'docs/ALPHA.md') -Destination (Join-Path $glauncherDestination 'README.md')
Write-Output "Portable app: $glauncherDestination"
