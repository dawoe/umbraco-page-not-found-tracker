<#
.SYNOPSIS
    Checks the relative links and anchors in the documentation.
.DESCRIPTION
    Also fails when a page of docs/ cannot be reached from docs/README.md. Checks every [text](target) in docs/**/*.md (docs/superpowers is internal and skipped), README.md and
    src/assets/readme.md. A relative target must exist, compared case-sensitively (a link that works on Windows
    must also work on Linux). A #anchor must match a heading of the target page, using GitHub's slug rules
    (duplicates get -1, -2, ...). http(s) and mailto links are skipped: external sites are flaky.
    Links inside fenced code blocks and inline code are ignored.
.PARAMETER RepoRoot
    The repository root. Defaults to the root of the repository this script lives in.
#>
param(
    [string] $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()
$slugCache = @{}
$edges = @{}

function Get-Lines {
    param([string] $Path)
    return [IO.File]::ReadAllText($Path) -split "\r?\n"
}

# Lines of a page outside fenced code blocks, with their 1-based numbers.
function Get-ProseLines {
    param([string[]] $Lines)

    $inFence = $false
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^\s*(```|~~~)') { $inFence = -not $inFence; continue }
        if (-not $inFence) { [pscustomobject]@{ Number = $i + 1; Text = $Lines[$i] } }
    }
}

function Get-Slugs {
    param([string] $Path)

    if ($slugCache.ContainsKey($Path)) { return $slugCache[$Path] }

    $slugs = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $seen = @{}
    foreach ($line in Get-ProseLines (Get-Lines $Path)) {
        if ($line.Text -notmatch '^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$') { continue }

        $text = $Matches[1]
        $text = [regex]::Replace($text, '\[([^\]]*)\]\([^)]*\)', '$1')
        $text = $text -replace '[`*]', ''
        $slug = ($text.ToLowerInvariant() -replace '[^\p{L}\p{N}\p{M} _-]', '') -replace ' ', '-'

        if ($seen.ContainsKey($slug)) {
            $seen[$slug]++
            [void] $slugs.Add("$slug-$($seen[$slug])")
        }
        else {
            $seen[$slug] = 0
            [void] $slugs.Add($slug)
        }
    }

    $slugCache[$Path] = $slugs
    return $slugs
}

# True when every segment of the relative path exists with exactly that casing.
function Test-ExactPath {
    param([string] $Base, [string] $Relative)

    $current = $Base
    foreach ($segment in ($Relative -split '/')) {
        if ($segment -eq '' -or $segment -eq '.') { continue }
        if ($segment -eq '..') { $current = Split-Path $current -Parent; continue }

        $match = Get-ChildItem -LiteralPath $current -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ceq $segment } | Select-Object -First 1
        if (-not $match) { return $null }
        $current = $match.FullName
    }

    return $current
}

$files = @()
$docs = Join-Path $RepoRoot 'docs'
if (Test-Path $docs) {
    $files += Get-ChildItem -Path $docs -Filter '*.md' -Recurse -File |
        Where-Object { ($_.FullName.Replace([string][char]92, '/') -split '/') -notcontains 'superpowers' }
}
foreach ($extra in @('README.md', 'src/assets/readme.md')) {
    $path = Join-Path $RepoRoot $extra
    if (Test-Path $path) { $files += Get-Item $path }
}

foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($RepoRoot, $file.FullName).Replace([string][char]92, '/')
    foreach ($line in Get-ProseLines (Get-Lines $file.FullName)) {
        $text = [regex]::Replace($line.Text, '`[^`]*`', '')
        foreach ($m in [regex]::Matches($text, '\[[^\]]*\]\((?<target>[^)\s]+)(?:\s+"[^"]*")?\)')) {
            $target = $m.Groups['target'].Value
            if ($target -match '^(https?:|mailto:|//)') { continue }

            $pathPart, $anchor = $target -split '#', 2
            $pathPart = [uri]::UnescapeDataString($pathPart)

            if ($pathPart -eq '') {
                $resolved = $file.FullName
            }
            else {
                $base = if ($pathPart.StartsWith('/')) { $RepoRoot } else { $file.DirectoryName }
                $resolved = Test-ExactPath $base $pathPart.TrimStart('/')
                if (-not $resolved) {
                    $failures.Add("${relative}:$($line.Number): link target not found: $target")
                    continue
                }
            }

            if ($resolved -like '*.md' -and (Test-Path $resolved -PathType Leaf)) {
                if (-not $edges.ContainsKey($file.FullName)) { $edges[$file.FullName] = [System.Collections.Generic.List[string]]::new() }
                $edges[$file.FullName].Add($resolved)
            }

            if ($anchor -and $resolved -like '*.md' -and (Test-Path $resolved -PathType Leaf)) {
                if (-not (Get-Slugs $resolved).Contains($anchor)) {
                    $failures.Add("${relative}:$($line.Number): anchor not found: $target")
                }
            }
        }
    }
}

# Every page of docs/ must be reachable from the index, directly or through another page.
$index = Join-Path $docs 'README.md'
if (Test-Path $index) {
    $reached = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $queue = [System.Collections.Generic.Queue[string]]::new()
    $queue.Enqueue((Get-Item $index).FullName)
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if (-not $reached.Add($current)) { continue }
        if ($edges.ContainsKey($current)) { foreach ($next in $edges[$current]) { $queue.Enqueue($next) } }
    }

    foreach ($page in $files | Where-Object { $_.FullName.StartsWith($docs) }) {
        if (-not $reached.Contains($page.FullName)) {
            $failures.Add("$([IO.Path]::GetRelativePath($RepoRoot, $page.FullName).Replace([string][char]92, '/')): not linked from docs/README.md (directly or through another page)")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ }
    Write-Host "$($failures.Count) documentation link problem(s)."
    exit 1
}

Write-Host "Documentation links are valid ($($files.Count) page(s) checked)."
