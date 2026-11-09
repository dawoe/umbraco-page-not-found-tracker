# Self-test for check-doc-samples.ps1. Builds fixture trees in a temp folder and asserts exit code and message.
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot '..' 'check-doc-samples.ps1'
$failures = 0

function Invoke-Case {
    param([string] $Name, [hashtable] $Files, [int] $ExpectedExit, [string] $ExpectedFragment = '')

    $root = Join-Path ([IO.Path]::GetTempPath()) ("docsamples-" + [guid]::NewGuid())
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

$source = "namespace X;`n// docs:begin one`nclass A`n{`n    int b;`n}`n// docs:end one`n"
$page = "# T`n`n<!-- sample: src/A.cs#one -->`n``````csharp`nclass A`n{`n    int b;`n}`n```````n"

Invoke-Case 'matching block' @{ 'src/A.cs' = $source; 'docs/p.md' = $page } 0
Invoke-Case 'block text differs' @{ 'src/A.cs' = $source; 'docs/p.md' = $page.Replace('int b;', 'int c;') } 1 'differs'
Invoke-Case 'block differs only in letter case' @{ 'src/A.cs' = $source; 'docs/p.md' = $page.Replace('int b;', 'INT b;') } 1 'differs'
Invoke-Case 'unknown id' @{ 'src/A.cs' = $source; 'docs/p.md' = $page.Replace('#one', '#two') } 1 'not found'
Invoke-Case 'missing file' @{ 'docs/p.md' = $page } 1 'not found'
Invoke-Case 'unmarked csharp block' @{ 'docs/p.md' = "# T`n`n``````csharp`nclass A {}`n```````n" } 1 'no sample'
Invoke-Case 'unmarked typescript block' @{ 'docs/p.md' = "# T`n`n``````typescript`nconst a = 1;`n```````n" } 1 'no sample'
Invoke-Case 'crlf source, lf page' @{ 'src/A.cs' = $source.Replace("`n", "`r`n"); 'docs/p.md' = $page } 0
Invoke-Case 'indented region' @{ 'src/A.cs' = "// docs:begin one`n    class A`n    {`n        int b;`n    }`n// docs:end one`n"; 'docs/p.md' = $page } 0
Invoke-Case 'blank lines around region ignored' @{ 'src/A.cs' = "// docs:begin one`n`nclass A`n{`n    int b;`n}`n`n// docs:end one`n"; 'docs/p.md' = $page } 0
Invoke-Case 'bash and json blocks ignored' @{ 'docs/p.md' = "# T`n`n``````bash`ndotnet build`n```````n``````json`n{}`n```````n" } 0
Invoke-Case 'superpowers folder ignored' @{ 'docs/superpowers/p.md' = "``````csharp`nclass A {}`n```````n" } 0

if ($failures -gt 0) { Write-Host "$failures case(s) failed"; exit 1 }
Write-Host 'All cases passed'
