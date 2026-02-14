#Requires -PSEdition Core

[CmdletBinding(DefaultParameterSetName = 'Build')]
param (
    [Parameter(ParameterSetName = 'Build')]
    [Parameter(ParameterSetName = 'Test')]
    [Parameter(ParameterSetName = 'Pack')]
    [string]$Configuration = 'Release',

    [Parameter(ParameterSetName = 'Test', Mandatory)]
    [switch]$Test,

    [Parameter(ParameterSetName = 'Test')]
    [switch]$NoBuild,

    [Parameter(ParameterSetName = 'Pack', Mandatory)]
    [switch]$Pack,

    [Parameter(ParameterSetName = 'Pack')]
    [string]$VersionSuffix)

$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot

try
{
    function Build {
        dotnet build -c $Configuration
        if ($LASTEXITCODE) { throw 'Build (baseline) failed' }

        dotnet build src/DocoptNet/DocoptNet.csproj -f netstandard2.0 -c $Configuration -p:RoslynVersion=4.4
        if ($LASTEXITCODE) { throw 'Build (Roslyn 4.4) failed' }
    }

    switch ($PSCmdlet.ParameterSetName) {
        'Build' {
            Build
        }
        'Test' {
            if (!$NoBuild) {
                Build
            }

            dotnet test --no-build -c $Configuration
            if ($LASTEXITCODE) { throw 'Test (baseline) failed' }

            dotnet test tests/DocoptNet.Tests/DocoptNet.Tests.csproj -c $Configuration -p:RoslynVersion=4.4
            if ($LASTEXITCODE) { throw 'Test (Roslyn 4.4) failed' }
        }
        'Pack' {
            Build

            $packArgs = @()
            if ($VersionSuffix) {
                $packArgs += @('--version-suffix', $VersionSuffix)
            }

            dotnet pack src/DocoptNet/DocoptNet.csproj --no-build -c $Configuration @packArgs
            if ($LASTEXITCODE) { throw 'Pack failed' }
        }
    }
}
finally
{
    Pop-Location
}
