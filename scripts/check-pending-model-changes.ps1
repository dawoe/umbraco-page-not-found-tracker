# Fails when PageNotFoundTrackerDbContext has model changes that no migration covers, for any tracking provider.
#
#   pwsh scripts/check-pending-model-changes.ps1
#
# Add the missing migration with scripts/add-migration.ps1 -Name <MigrationName>.

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$codeDir = Join-Path $repoRoot 'src/code'
$providers = @(
    'Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite',
    'Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer'
)

Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet tool restore failed.'
    }

    $failed = @()
    foreach ($provider in $providers) {
        $project = Join-Path (Join-Path $codeDir $provider) "$provider.csproj"
        Write-Host "Checking pending model changes for $provider"
        dotnet ef migrations has-pending-model-changes --project $project
        if ($LASTEXITCODE -ne 0) {
            $failed += $provider
        }
    }

    if ($failed.Count -gt 0) {
        throw "Pending model changes (or a failed check) in: $($failed -join ', '). Run scripts/add-migration.ps1 -Name <MigrationName>."
    }

    Write-Host 'No pending model changes.'
}
finally {
    Pop-Location
}
