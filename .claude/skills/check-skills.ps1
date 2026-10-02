$ErrorActionPreference = 'Stop'

$skillsDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $skillsDir '../..')).Path
$projectRoot = Join-Path $repoRoot 'src/Assets/_Project'
$packageName = 'games.engine-room.foundation'
$packageCitation = "src/Packages/$packageName"
$packageRoot = Join-Path $repoRoot $packageCitation
$errors = 0
$notes = 0
$extensions = '\.(cs|md|asmdef|asmref|unity|prefab|asset|json|inputactions|mixer|rsp|sh|ps1|txt|config)$'
$packagePathPattern = '^(Core|Shared|Bootstrap|Domains/Loading|Domains/Settings)(/|$)'

if (-not (Test-Path -LiteralPath $packageRoot -PathType Container))
{
    $packageRoot = $null
    $packageCache = Join-Path $repoRoot 'src/Library/PackageCache'

    if (Test-Path -LiteralPath $packageCache -PathType Container)
    {
        $newest = Get-ChildItem -LiteralPath $packageCache -Directory -Filter "$packageName@*" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

        if ($newest)
        {
            $packageRoot = $newest.FullName
        }
    }
}

if (-not $packageRoot)
{
    Write-Output 'note  foundation package not resolved (not embedded, not in src/Library/PackageCache); open the project in Unity once. Package paths are not checked.'
    $notes++
}

function Test-SkillPath([string]$path, [string]$here)
{
    $isCitation = $path -eq $packageCitation -or $path.StartsWith("$packageCitation/")

    if ($packageRoot -and $isCitation)
    {
        $rest = $path.Substring($packageCitation.Length).TrimStart('/')
        $target = if ($rest) { Join-Path $packageRoot $rest } else { $packageRoot }

        if (Test-Path -LiteralPath $target)
        {
            return $true
        }
    }

    $roots = @($here, $repoRoot, $projectRoot, (Join-Path $repoRoot 'src'))

    if ($packageRoot)
    {
        $roots += $packageRoot
    }

    foreach ($root in $roots)
    {
        if (Test-Path -LiteralPath (Join-Path $root $path))
        {
            return $true
        }
    }

    return (-not $packageRoot) -and ($isCitation -or $path -match $packagePathPattern)
}

function Test-IsPath([string]$token)
{
    if ($token -match '[\s<>*{}$(|]' -or $token.StartsWith('http'))
    {
        return $false
    }

    if (-not $token.TrimEnd('/').Contains('/'))
    {
        return $false
    }

    return $token.EndsWith('/') -or $token -match $extensions
}

foreach ($skill in Get-ChildItem -LiteralPath $skillsDir -Directory)
{
    $file = Join-Path $skill.FullName 'SKILL.md'

    if (-not (Test-Path -LiteralPath $file))
    {
        Write-Output "ERROR $($skill.Name): missing SKILL.md"
        $errors++
        continue
    }

    $head = Get-Content -LiteralPath $file -TotalCount 5

    if (-not ($head -contains "name: $($skill.Name)"))
    {
        Write-Output "ERROR $($skill.Name)/SKILL.md: frontmatter 'name' must be '$($skill.Name)'"
        $errors++
    }

    if (-not ($head | Where-Object { $_ -match '^description: .{40,}' }))
    {
        Write-Output "ERROR $($skill.Name)/SKILL.md: missing or too short 'description'"
        $errors++
    }
}

$markdownFiles = @(Get-ChildItem -LiteralPath $skillsDir -Recurse -File -Filter '*.md' | Sort-Object FullName) + @(Get-Item -LiteralPath (Join-Path $repoRoot 'docs/Architecture.md'))

foreach ($md in $markdownFiles)
{
    $relative = $md.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
    $lineNumber = 0

    foreach ($line in Get-Content -LiteralPath $md.FullName)
    {
        $lineNumber++

        foreach ($match in [regex]::Matches($line, '`([^`]*)`'))
        {
            $token = $match.Groups[1].Value

            if (-not (Test-IsPath $token) -or (Test-SkillPath $token $md.DirectoryName))
            {
                continue
            }

            if ($md.Name -eq 'examples.md' -or $line -match '(?i)if present')
            {
                Write-Output "note  ${relative}:${lineNumber}: example path not found: $token"
                $notes++
            }
            else
            {
                Write-Output "ERROR ${relative}:${lineNumber}: path not found: $token"
                $errors++
            }
        }
    }
}

Write-Output "check-skills: $errors error(s), $notes note(s)"

if ($errors -gt 0)
{
    exit 1
}
