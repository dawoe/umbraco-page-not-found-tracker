# Adds an EF Core migration of PageNotFoundTrackerDbContext to every tracking provider project.
#
#   pwsh scripts/add-migration.ps1 -Name <MigrationName>
#
# Each provider project has a design-time factory, so no running site or database is needed.
# The build stays at zero analyzer warnings because src/.editorconfig marks the provider Migrations/*.cs as generated code
# (StyleCop and the missing-XML-doc warning CS1591 skip generated code). The script still prepends the StyleCop
# copyright header, the project convention for every C# file, to each new file and to the rewritten model snapshot.
param(
    [Parameter(Mandatory = $true)]
    [string] $Name
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$codeDir = Join-Path $repoRoot 'src/code'
$providers = @(
    'Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite',
    'Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer'
)

$header = '// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.'

Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet tool restore failed.'
    }

    foreach ($provider in $providers) {
        $projectDir = Join-Path $codeDir $provider
        $project = Join-Path $projectDir "$provider.csproj"
        $migrationsDir = Join-Path $projectDir 'Migrations'

        $before = @()
        if (Test-Path $migrationsDir) {
            $before = @(Get-ChildItem -Path $migrationsDir -Filter '*.cs' | ForEach-Object { $_.FullName })
        }

        Write-Host "Adding migration '$Name' to $provider"
        dotnet ef migrations add $Name --project $project --output-dir Migrations
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet ef migrations add failed for $provider."
        }

        # New files plus the model snapshot, which dotnet ef rewrites on every migration.
        $files = @(Get-ChildItem -Path $migrationsDir -Filter '*.cs' |
            Where-Object { ($before -notcontains $_.FullName) -or ($_.Name -like '*ModelSnapshot.cs') })

        foreach ($file in $files) {
            $content = [System.IO.File]::ReadAllText($file.FullName)
            if (-not $content.StartsWith($header)) {
                [System.IO.File]::WriteAllText($file.FullName, $header + "`r`n`r`n" + $content, [System.Text.UTF8Encoding]::new($false))
                Write-Host "  added the copyright header to $($file.Name)"
            }
        }
    }
}
finally {
    Pop-Location
}
