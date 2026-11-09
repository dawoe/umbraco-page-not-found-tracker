# Self-test for check-doc-links.ps1. Builds fixture trees in a temp folder and asserts exit code and message.
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot '..' 'check-doc-links.ps1'
$failures = 0

function Invoke-Case {
    param([string] $Name, [hashtable] $Files, [int] $ExpectedExit, [string] $ExpectedFragment = '')

    $root = Join-Path ([IO.Path]::GetTempPath()) ("doclinks-" + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $root | Out-Null
    foreach ($relative in $Files.Keys) {
        $path = Join-Path $root $relative
        New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
        [IO.File]::WriteAllText($path, $Files[$relative])
    }

    $output = & pwsh -NoProfile -File $script -RepoRoot $root 2>&1 | Out-String
    $exit = $LASTEXITCODE
    Remove-Item $root -Recurse -Force

    $ok = ($exit -eq $ExpectedExit) -and ($output -like "*$ExpectedFragment*")
    if ($ok) {
        Write-Host "PASS $Name"
    }
    else {
        Write-Host "FAIL $Name (exit $exit, expected $ExpectedExit; expected fragment '$ExpectedFragment')`n$output"
        $script:failures++
    }
}

$b = '```'

Invoke-Case 'valid relative link' @{ 'docs/a.md' = "# A`n[b](b.md)`n"; 'docs/b.md' = "# B`n" } 0
Invoke-Case 'link to a folder' @{ 'docs/a.md' = "# A`n[e](extending/)`n"; 'docs/extending/README.md' = "# E`n" } 0
Invoke-Case 'missing file' @{ 'docs/a.md' = "# A`n[b](nope.md)`n" } 1 'not found'
Invoke-Case 'wrong-case file name' @{ 'docs/a.md' = "# A`n[b](B.md)`n"; 'docs/b.md' = "# B`n" } 1 'not found'
Invoke-Case 'valid anchor in same file' @{ 'docs/a.md' = "# A`n## Two words`n[x](#two-words)`n" } 0
Invoke-Case 'broken anchor in same file' @{ 'docs/a.md' = "# A`n[x](#nope)`n" } 1 'anchor'
Invoke-Case 'valid anchor in another file' @{ 'docs/a.md' = "# A`n[x](b.md#section-one)`n"; 'docs/b.md' = "# B`n## Section one`n" } 0
Invoke-Case 'broken anchor in another file' @{ 'docs/a.md' = "# A`n[x](b.md#nope)`n"; 'docs/b.md' = "# B`n" } 1 'anchor'
Invoke-Case 'anchor with code and punctuation in heading' @{ 'docs/a.md' = "# A`n## The ``Foo`` setting (v2)`n[x](#the-foo-setting-v2)`n" } 0
Invoke-Case 'duplicate heading gets -1' @{ 'docs/a.md' = "# A`n## Same`n## Same`n[x](#same)`n[y](#same-1)`n" } 0
Invoke-Case 'external and mailto links skipped' @{ 'docs/a.md' = "# A`n[x](https://example.invalid/nope)`n[y](mailto:a@b.c)`n" } 0
Invoke-Case 'link inside fenced code ignored' @{ 'docs/a.md' = "# A`n${b}md`n[x](nope.md)`n${b}`n" } 0
Invoke-Case 'link inside inline code ignored' @{ 'docs/a.md' = "# A`n``[x](nope.md)``" } 0
Invoke-Case 'root README and nuget readme are checked' @{ 'README.md' = "# R`n[d](docs/nope.md)`n"; 'docs/a.md' = "# A`n" } 1 'README.md'
Invoke-Case 'page not linked from the index fails' @{ 'docs/README.md' = "# Index`n[a](a.md)`n"; 'docs/a.md' = "# A`n"; 'docs/orphan.md' = "# O`n" } 1 'not linked from docs/README.md'
Invoke-Case 'page linked through a sub-index passes' @{ 'docs/README.md' = "# Index`n[e](extending/README.md)`n"; 'docs/extending/README.md' = "# E`n[x](x.md)`n"; 'docs/extending/x.md' = "# X`n" } 0
Invoke-Case 'superpowers folder not checked' @{ 'docs/superpowers/s.md' = "[x](nope.md)`n"; 'docs/a.md' = "# A`n" } 0

if ($failures -gt 0) { Write-Host "$failures case(s) failed"; exit 1 }
Write-Host 'All cases passed'
