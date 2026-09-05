param(
    [Parameter(Mandatory = $true)]
    [string]$ClientExePath,
    [string]$ServerUrl = "http://127.0.0.1:7333",
    [string]$ProfileA = "PlayerA",
    [string]$ProfileB = "PlayerB",
    [int]$ScreenWidth = 960,
    [int]$ScreenHeight = 540,
    [switch]$NoServerUrlArgument
)

$ErrorActionPreference = "Stop"

function Start-Project333Client {
    param(
        [string]$Profile,
        [string]$ClientExePath,
        [string]$ServerUrl,
        [int]$ScreenWidth,
        [int]$ScreenHeight,
        [bool]$NoServerUrlArgument
    )

    $arguments = @(
        "-project333Profile", $Profile,
        "-screen-fullscreen", "0",
        "-screen-width", "$ScreenWidth",
        "-screen-height", "$ScreenHeight"
    )

    if (-not $NoServerUrlArgument) {
        $arguments += @("-project333ServerUrl", $ServerUrl)
    }

    Write-Host "[client] launching profile=$Profile server=$ServerUrl"
    Start-Process `
        -FilePath $ClientExePath `
        -WorkingDirectory (Split-Path -Parent $ClientExePath) `
        -ArgumentList $arguments
}

if (-not (Test-Path -LiteralPath $ClientExePath)) {
    throw "Client exe was not found: $ClientExePath"
}

Start-Project333Client `
    -Profile $ProfileA `
    -ClientExePath $ClientExePath `
    -ServerUrl $ServerUrl `
    -ScreenWidth $ScreenWidth `
    -ScreenHeight $ScreenHeight `
    -NoServerUrlArgument $NoServerUrlArgument.IsPresent

Start-Sleep -Seconds 1

Start-Project333Client `
    -Profile $ProfileB `
    -ClientExePath $ClientExePath `
    -ServerUrl $ServerUrl `
    -ScreenWidth $ScreenWidth `
    -ScreenHeight $ScreenHeight `
    -NoServerUrlArgument $NoServerUrlArgument.IsPresent

Write-Host "[client] launched two After333 clients."
