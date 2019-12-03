[CmdletBinding()]
Param(
    [string]
    $UnityVersion = "2019.2",
    [switch]
    $Trace = $false
)

Set-StrictMode -Version Latest
if ($Trace) {
    Set-PSDebug -Trace 1
}

. $PSScriptRoot\helpers.ps1 | out-null

$srcdir = Join-Path $rootDirectory 'src'

Get-ChildItem -Directory $srcdir | % {
    if (Test-Path "$srcDir\$($_)\package.json") {
        Write-Output "Testing $($_.Name)"

        $packageDir = Join-Path $srcdir $_.Name
        Invoke-Command -Fatal { & upm-ci package test --package-path $packageDir -u $UnityVersion }
    }
}
