[CmdletBinding()]
Param(
    [switch]
    $Trace = $false
)

Set-StrictMode -Version Latest
if ($Trace) {
    Set-PSDebug -Trace 1
}

. $PSScriptRoot\helpers.ps1 | out-null

$packagesDir = Join-Path $rootDirectory 'build\nuget\Release'
$publishUrl = "https://artifactory.internal.unity3d.com/api/nuget/core-utilities"

try {
    Push-Location $packagesDir

    Get-ChildItem -File *.nupkg | %{ 
        Write-Output "Publishing $($_.Name) to $($publishUrl)"
        Invoke-Command -Fatal { & dotnet nuget push $_.Name -k $($env:ARTIFACTORY) -s $publishUrl }
    }

} finally {
    Pop-Location
}

$packagesDir = Join-Path $rootDirectory 'upm-ci~\packages'
$publishUrl = "https://artifactory.internal.unity3d.com/api/npm/core-npm"
$authUrl = "https://artifactory.internal.unity3d.com/api/npm/auth"

$npmrc="$($env:USERPROFILE)\.npmrc"
$npmrcBackup="$($env:USERPROFILE)\.npmrc-backup"
$DoNpmRcBackup = Test-Path $npmrc

try {
    Push-Location $packagesDir

    if ($DoNpmRcBackup) {
        Write-Output "Backing up $npmrc to $npmrcBackup"
        Move-Item $npmrc $npmrcBackup -Force -ErrorAction SilentlyContinue
    }

    $bytes = [System.Text.Encoding]::ASCII.GetBytes($env:ARTIFACTORY)
    $base64 = [System.Convert]::ToBase64String($bytes)
    $basicAuthValue = "Basic $base64"
    $headers = @{ Authorization = $basicAuthValue }

    Invoke-WebRequest -usebasicparsing -Headers $headers -Uri $authUrl -OutFile $npmrc

    Get-ChildItem -File *.tgz | %{ 
        Write-Output "Publishing $($_.Name) to $($publishUrl)"
        Invoke-Command -Fatal { & npm publish $($_.Name) --quiet --registry $publishUrl }
    }

} finally {
    Pop-Location
    if ($DoNpmRcBackup) {
        Write-Output "Restoring $npmrc"
        Move-Item $npmrcBackup $npmrc -Force -ErrorAction SilentlyContinue
    } else {
        Remove-Item $npmrc
    }
}

