<#
.SYNOPSIS
Build LinkpearlDev and drop it into a Dalamud dev-plugin folder so the handset reloads in-game.

.DESCRIPTION
Builds src/Linkpearl.Host in Debug (assembly name LinkpearlDev), mirrors bin/Debug into the
dev-plugin folder, and bumps the mtime on LinkpearlDev.dll so Dalamud's file watcher reloads the
plugin without a manual disable/enable in /xlplugins.

The LAME encoder natives stay loaded by the game process while the plugin is running, so copying
over them fails with a sharing violation. They never change, so they are skipped after the first
install.

.PARAMETER Destination
The Dalamud dev-plugin folder. Defaults to $env:LINKPEARL_DEV_PLUGIN.

.PARAMETER DalamudHome
The Dalamud dev install the build references. Defaults to the XIVLauncher addon hooks folder.

.EXAMPLE
./tools/install-dev-plugin.ps1 -Destination 'C:\Users\you\Desktop\Linkpearl2_New\LinkpearlDev'
#>
[CmdletBinding()]
param(
    [string]$Destination = $env:LINKPEARL_DEV_PLUGIN,
    [string]$DalamudHome = (Join-Path $env:AppData 'XIVLauncher\addon\Hooks\dev'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Destination)) {
    throw "No dev-plugin folder. Pass -Destination, or set LINKPEARL_DEV_PLUGIN once with: setx LINKPEARL_DEV_PLUGIN '<path to your LinkpearlDev folder>'"
}

if (-not (Test-Path -LiteralPath $DalamudHome)) {
    throw "DALAMUD_HOME is not a directory: $DalamudHome"
}

$root = Split-Path -Parent $PSScriptRoot
$env:DALAMUD_HOME = $DalamudHome

if (-not $SkipBuild) {
    dotnet build (Join-Path $root 'src\Linkpearl.Host') -c Debug
    if ($LASTEXITCODE -ne 0) {
        throw "Debug build failed; nothing was copied."
    }
}

$out = Join-Path $root 'src\Linkpearl.Host\bin\Debug'
$dll = Join-Path $out 'LinkpearlDev.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    throw "LinkpearlDev.dll not found under $out"
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null

$skipped = 0
foreach ($file in Get-ChildItem -LiteralPath $out -Recurse -File) {
    if ($file.Name -like 'libmp3lame*') {
        if (Test-Path -LiteralPath (Join-Path $Destination $file.Name)) {
            $skipped++
            continue
        }
    }

    $relative = $file.FullName.Substring($out.Length).TrimStart('\')
    $target = Join-Path $Destination $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}

# Dalamud reloads a dev plugin when the assembly's LastWrite moves, and Copy-Item preserves the
# source timestamp, so an unchanged rebuild would otherwise look untouched.
(Get-Item -LiteralPath (Join-Path $Destination 'LinkpearlDev.dll')).LastWriteTime = Get-Date

Write-Host "LinkpearlDev installed to $Destination ($skipped locked encoder file(s) left in place)."
Write-Host "The phone restarts on its own; no /xlplugins toggle needed."
