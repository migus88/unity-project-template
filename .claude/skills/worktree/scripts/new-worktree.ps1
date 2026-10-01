# Create a git worktree for parallel agent work, seed its Unity Library from the main checkout
# and open a new Unity Editor (with UI) on its src/.
# Usage: pwsh -File .claude/skills/worktree/scripts/new-worktree.ps1 <name> [-Base <ref>] [-NoOpen] [-TimeoutSeconds 900]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$Name,
    [string]$Base = 'HEAD',
    [switch]$NoOpen,
    [int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'

$main = ((git worktree list --porcelain | Select-Object -First 1) -replace '^worktree ', '')
$main = (Resolve-Path $main).Path
$parent = Join-Path (Split-Path $main -Parent) ((Split-Path $main -Leaf) + '-worktrees')
$dir = Join-Path $parent $Name
$branch = "wt/$Name"
if (Test-Path $dir) { throw "already exists: $dir" }

Write-Host "== git worktree add $dir ($branch from $Base)"
New-Item -ItemType Directory -Force -Path $parent | Out-Null
git -C $main worktree add -b $branch $dir $Base
if ($LASTEXITCODE -ne 0) { throw 'git worktree add failed' }
$src = Join-Path $dir 'src'

$link = Join-Path $src '.claude'
$item = Get-Item $link -Force -ErrorAction SilentlyContinue
if (-not $item -or -not $item.LinkType) {
    if ($item) { Remove-Item $link -Force -Recurse }
    New-Item -ItemType Junction -Path $link -Target (Join-Path $dir '.claude') | Out-Null
}

function Copy-Tree($from, $to, [string[]]$excludeDirs = @(), [string[]]$excludeFiles = @()) {
    if (-not (Test-Path $from)) { return }
    $rc = @($from, $to, '/E', '/MT:16', '/R:1', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($excludeDirs.Count) { $rc += '/XD'; $rc += $excludeDirs }
    if ($excludeFiles.Count) { $rc += '/XF'; $rc += $excludeFiles }
    robocopy @rc | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE): $from" }
}

Write-Host "== seeding Library from $main\src\Library"
$sw = [Diagnostics.Stopwatch]::StartNew()
$lib = Join-Path $main 'src\Library'
Copy-Tree $lib (Join-Path $src 'Library') `
    @((Join-Path $lib 'Bee'), (Join-Path $lib 'BurstCache'), (Join-Path $lib 'BuildHistory'), (Join-Path $lib 'PackageManager'), (Join-Path $lib 'Pipeline')) `
    @('ArtifactDB-lock', 'SourceAssetDB-lock', 'EditorInstance.json', 'ProtocolInstance.json', 'burst.pid', 'ilpp.pid')
Copy-Tree (Join-Path $main 'src\Packages\nuget-packages\InstalledPackages') (Join-Path $src 'Packages\nuget-packages\InstalledPackages')
Copy-Tree (Join-Path $main 'src\UserSettings') (Join-Path $src 'UserSettings')
Write-Host "   copied in $([int]$sw.Elapsed.TotalSeconds) s"

Write-Host "worktree: $dir"
Write-Host "branch:   $branch"
Write-Host "project:  $src"
if ($NoOpen) { return }

$version = ((Get-Content (Join-Path $main 'src\ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -like 'm_EditorVersion:*' }) -replace '^m_EditorVersion:\s*', '')
Write-Host "== opening Unity $version on $src"
$sw.Restart()
$unityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
New-Item -ItemType Directory -Force -Path (Join-Path $src 'Logs') | Out-Null
$log = Join-Path $src 'Logs\Editor.log'
if (Test-Path $unityExe) {
    Start-Process -FilePath $unityExe -ArgumentList @('-projectPath', "`"$src`"", '-logFile', "`"$log`"") -WindowStyle Minimized
} else {
    unity open $src --no-banner --args "-logFile `"$log`""
}

function Fail($what) { throw "$what after $TimeoutSeconds s; check $log and the Editor window (Safe Mode, dialog?)" }

Write-Host "== waiting for 'unity status' to list it ready"
while ($true) {
    $row = (unity status --format tsv --no-banner 2>$null) | Where-Object { (($_ -split "`t")[2] -replace '\\', '/') -eq ($src -replace '\\', '/') } | Select-Object -First 1
    if ($row -and ($row -split "`t")[1] -eq 'ready') {
        $c = $row -split "`t"
        Write-Host "   ready after $([int]$sw.Elapsed.TotalSeconds) s: port $($c[0]), pid $($c[4])"
        break
    }
    if ($sw.Elapsed.TotalSeconds -ge $TimeoutSeconds) { Fail 'not listed ready' }
    Start-Sleep -Seconds 5
}

Write-Host '== waiting for the startup import/compile to settle (main-thread commands answer)'
while ($true) {
    $out = (unity command eval --no-banner --project-path $src --timeout 20 --code 'return "settled";' 2>$null) | Out-String
    if ($out -match '"result":"settled"') { Write-Host "   settled after $([int]$sw.Elapsed.TotalSeconds) s"; break }
    if ($sw.Elapsed.TotalSeconds -ge $TimeoutSeconds) { Fail 'main-thread commands still busy' }
    Start-Sleep -Seconds 5
}
Write-Host "drive it with: unity command <name> --no-banner --project-path $src"
