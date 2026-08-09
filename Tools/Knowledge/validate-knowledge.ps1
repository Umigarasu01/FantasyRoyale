$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$knowledgeRoot = Join-Path $repositoryRoot 'docs\fantasy-royale'
$errors = New-Object System.Collections.Generic.List[string]

foreach ($required in @('index.md', 'PLANS.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $knowledgeRoot $required))) {
        $errors.Add("必須Fileがありません: $required")
    }
}

$markdownFiles = Get-ChildItem -LiteralPath $knowledgeRoot -Recurse -File -Filter '*.md'
$directories = Get-ChildItem -LiteralPath $knowledgeRoot -Recurse -Directory
foreach ($directory in $directories) {
    if ($directory.Name -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
        $errors.Add("Directory名がkebab-caseではありません: $($directory.FullName)")
    }
}

foreach ($file in $markdownFiles) {
    if ($file.Name -ne 'PLANS.md' -and $file.BaseName -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
        $errors.Add("Markdown File名がkebab-caseではありません: $($file.FullName)")
    }

    $content = [IO.File]::ReadAllText($file.FullName)
    if ($content.Contains('[[')) {
        $errors.Add("WikiLinkが残っています: $($file.FullName)")
    }

    foreach ($match in [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')) {
        $link = $match.Groups[1].Value.Split('#')[0]
        if ([string]::IsNullOrWhiteSpace($link) -or
            $link.StartsWith('#') -or
            $link -match '^[a-zA-Z]+:') {
            continue
        }

        $decodedLink = [Uri]::UnescapeDataString($link)
        $target = if ([IO.Path]::IsPathRooted($decodedLink)) {
            $decodedLink
        }
        else {
            Join-Path $file.DirectoryName ($decodedLink -replace '/', '\')
        }

        if (-not (Test-Path -LiteralPath $target)) {
            $errors.Add("解決できないMarkdown Link: $($file.FullName) -> $link")
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Knowledge validation passed: $($markdownFiles.Count) Markdown files"
