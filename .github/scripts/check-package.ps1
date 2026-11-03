# Asserts that the packed NuGet package is complete.
# Guards against a stale --no-build manifest producing a package without the
# assemblies or the compiled backoffice client assets.
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$packages = @(Get-ChildItem -Path $PackageDirectory -Filter '*.nupkg' -Recurse |
    Where-Object { $_.Name -notlike '*.symbols.nupkg' })

if ($packages.Count -ne 1) {
    throw "Expected exactly one .nupkg in '$PackageDirectory' but found $($packages.Count): $($packages.Name -join ', ')"
}

$expected = @(
    'lib/net10.0/Umbraco.Community.PageNotFoundTracker.dll',
    'lib/net10.0/Umbraco.Community.PageNotFoundTracker.Core.dll',
    'lib/net10.0/Umbraco.Community.PageNotFoundTracker.Infrastructure.dll',
    'lib/net10.0/Umbraco.Community.PageNotFoundTracker.BackOffice.dll',
    'lib/net10.0/Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite.dll',
    'lib/net10.0/Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer.dll',
    'staticwebassets/App_Plugins/PageNotFoundTracker/entry-point.js'
)

$zip = [System.IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName })
}
finally {
    $zip.Dispose()
}

$missing = @($expected | Where-Object { $entries -notcontains $_ })
if ($missing.Count -gt 0) {
    Write-Host "Package entries:"
    $entries | ForEach-Object { Write-Host "  $_" }
    throw "Package '$($packages[0].Name)' is missing: $($missing -join ', ')"
}

Write-Host "Package '$($packages[0].Name)' contains all expected files."
