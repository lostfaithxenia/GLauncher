param(
    [ValidateSet('WinUI','Wpf')][string]$Framework = 'WinUI',
    [Parameter(Mandatory=$true)][string]$VideoPath,
    [string]$DotnetPath = 'dotnet',
    [string]$OutputDirectory = '',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/rendering-spike' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$video = (Resolve-Path -LiteralPath $VideoPath).Path
$dotnetCommand = Get-Command $DotnetPath -ErrorAction Stop
$env:DOTNET_ROOT = Split-Path $dotnetCommand.Source -Parent
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_HOME = Join-Path $outputRoot 'cli-home'
$env:APPDATA = Join-Path $outputRoot 'roaming'
$env:LOCALAPPDATA = Join-Path $outputRoot 'local'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME,$env:APPDATA,$env:LOCALAPPDATA | Out-Null
$directory = if ($Framework -eq 'WinUI') { 'winui' } else { 'wpf' }
$project = Join-Path $PSScriptRoot "$directory/GLauncher.RenderingSpike.$Framework.csproj"
if (-not $NoBuild) {
    $restoreConfig = Join-Path $PSScriptRoot 'NuGet.Config'
    & $dotnetCommand.Source build $project -c Release --nologo "-p:RestoreConfigFile=$restoreConfig"
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$samples = Join-Path $outputRoot "$Framework-$stamp.jsonl"
if ($Framework -eq 'WinUI') {
    $exe = Join-Path $PSScriptRoot 'winui/bin/Release/net10.0-windows10.0.19041.0/win-x64/GLauncher.RenderingSpike.WinUI.exe'
    $arguments = @(('"'+$video+'"'),('"'+$samples+'"'),'--automated')
    $probe = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Normal -Wait -PassThru -RedirectStandardOutput ($samples+'.stdout.txt') -RedirectStandardError ($samples+'.stderr.txt')
    $exitCode = $probe.ExitCode
} else {
    $dll = Join-Path $PSScriptRoot 'wpf/bin/Release/net10.0-windows/GLauncher.RenderingSpike.Wpf.dll'
    & $dotnetCommand.Source $dll $video $samples --automated
    $exitCode = $LASTEXITCODE
}
Write-Output "Samples: $samples"
Write-Output "Process exit: $exitCode"
if ($exitCode -ne 0) { throw 'Probe runtime failed; preserve samples and error logs' }
$node = Get-Command node -ErrorAction SilentlyContinue
if ($node) {
    & $node.Source (Join-Path $PSScriptRoot 'analyze.mjs') $samples ($samples+'.summary.json')
    if ($LASTEXITCODE -ne 0) { throw 'Observed lifecycle validation failed' }
} else {
    Write-Output 'Node is optional. Use analyze.mjs later to summarize the JSONL samples.'
}
