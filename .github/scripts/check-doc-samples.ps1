<#
.SYNOPSIS
    Fails when a code block quoted in docs/ differs from the compiled sample it quotes.
.DESCRIPTION
    A page quotes a region of a source file with a comment on the line before a fenced block:
        <!-- sample: <repo-relative path>#<id> -->
    The region is the lines between "docs:begin <id>" and "docs:end <id>" in that file (marker lines excluded,
    leading and trailing blank lines ignored, common indentation removed, CRLF and LF treated alike).
    A csharp or typescript block without that comment also fails. Blocks tagged bash, json, xml and untagged ones
    are not checked. docs/superpowers (internal specs and plans) is not checked.
.PARAMETER RepoRoot
    The repository root. Defaults to the root of the repository this script lives in.
.PARAMETER DocsRoot
    The folder with the pages. Defaults to docs under the repository root.
#>
param(
    [string] $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [string] $DocsRoot
)

$ErrorActionPreference = 'Stop'
if (-not $DocsRoot) { $DocsRoot = Join-Path $RepoRoot 'docs' }

$failures = [System.Collections.Generic.List[string]]::new()
$checkedTags = @('csharp', 'cs', 'typescript', 'ts')

function Format-Block {
    param([string[]] $Lines)

    $trimmed = @($Lines | ForEach-Object { $_.TrimEnd() })
    $start = 0
    $end = $trimmed.Count - 1
    while ($start -le $end -and $trimmed[$start] -eq '') { $start++ }
    while ($end -ge $start -and $trimmed[$end] -eq '') { $end-- }
    if ($end -lt $start) { return '' }

    $body = $trimmed[$start..$end]
    $indent = ($body | Where-Object { $_ -ne '' } | ForEach-Object { $_.Length - $_.TrimStart().Length } | Measure-Object -Minimum).Minimum
    return (($body | ForEach-Object { if ($_ -eq '') { '' } else { $_.Substring($indent) } }) -join "`n")
}

function Get-Region {
    param([string] $Path, [string] $Id)

    $lines = [IO.File]::ReadAllText($Path) -split "\r?\n"
    $begin = -1
    $end = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "docs:begin\s+$([regex]::Escape($Id))\s*$") { $begin = $i }
        elseif ($lines[$i] -match "docs:end\s+$([regex]::Escape($Id))\s*$") { $end = $i }
    }
    if ($begin -lt 0 -or $end -le $begin) { return $null }
    if ($end -eq $begin + 1) { return '' }
    return Format-Block $lines[($begin + 1)..($end - 1)]
}

$pages = Get-ChildItem -Path $DocsRoot -Filter '*.md' -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { ($_.FullName.Replace([string][char]92, '/') -split '/') -notcontains 'superpowers' }

foreach ($page in $pages) {
    $relative = [IO.Path]::GetRelativePath($RepoRoot, $page.FullName).Replace('\', '/')
    $lines = [IO.File]::ReadAllText($page.FullName) -split "\r?\n"
    $i = 0
    while ($i -lt $lines.Count) {
        if ($lines[$i] -notmatch '^\s*```(\w*)') { $i++; continue }

        $tag = $Matches[1].ToLowerInvariant()
        $fenceLine = $i + 1
        $previous = if ($i -gt 0) { $lines[$i - 1] } else { '' }
        $blockLines = @()
        $i++
        while ($i -lt $lines.Count -and $lines[$i] -notmatch '^\s*```\s*$') { $blockLines += $lines[$i]; $i++ }
        $i++

        if ($previous -match '^\s*<!--\s*sample:\s*(?<path>[^#]+?)#(?<id>\S+?)\s*-->\s*$') {
            $sourcePath = Join-Path $RepoRoot $Matches['path']
            if (-not (Test-Path $sourcePath -PathType Leaf)) {
                $failures.Add("${relative}:${fenceLine}: sample file not found: $($Matches['path'])")
                continue
            }

            $expected = Get-Region $sourcePath $Matches['id']
            if ($null -eq $expected) {
                $failures.Add("${relative}:${fenceLine}: region '$($Matches['id'])' not found in $($Matches['path'])")
            }
            elseif ($expected -cne (Format-Block $blockLines)) {
                $failures.Add("${relative}:${fenceLine}: block differs from region '$($Matches['id'])' in $($Matches['path'])")
            }
        }
        elseif ($checkedTags -contains $tag) {
            $failures.Add("${relative}:${fenceLine}: $tag block has no sample comment (<!-- sample: path#id -->)")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ }
    Write-Host "$($failures.Count) documentation sample problem(s)."
    exit 1
}

Write-Host "Documentation samples are in step ($(@($pages).Count) page(s) checked)."
