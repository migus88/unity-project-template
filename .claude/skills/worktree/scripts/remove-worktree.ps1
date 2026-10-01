# Close the worktree's Unity Editor, remove the worktree (with its Library) and optionally its branch.
# Usage: pwsh -File .claude/skills/worktree/scripts/remove-worktree.ps1 <name> [-DeleteBranch] [-Force]
#   -DeleteBranch  git branch -d wt/<name> (refuses if unmerged; with -Force: -D)
#   -Force         remove even with uncommitted changes, and force-delete the branch
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [switch]$DeleteBranch,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'

$main = ((git worktree list --porcelain | Select-Object -First 1) -replace '^worktree ', '')
$main = (Resolve-Path $main).Path
$parent = Join-Path (Split-Path $main -Parent) ((Split-Path $main -Leaf) + '-worktrees')
$dir = Join-Path $parent $Name
$src = Join-Path $dir 'src'
$branch = "wt/$Name"

if (Test-Path $dir) {
    if (-not $Force -and (git -C $dir status --porcelain)) {
        git -C $dir status --short
        throw 'worktree has uncommitted changes; commit them or pass -Force'
    }

    $row = (unity status --format tsv --no-banner 2>$null) |
        Where-Object { (($_ -split "`t")[2] -replace '\\', '/') -eq ($src -replace '\\', '/') } | Select-Object -First 1
    $editorPid = if ($row) { [int](($row -split "`t")[4]) } else { $null }
    if (-not $editorPid) {
        $instance = Join-Path $src 'Library\EditorInstance.json'
        if (Test-Path $instance) { $editorPid = (Get-Content $instance -Raw | ConvertFrom-Json).process_id }
    }
    $proc = if ($editorPid) { Get-Process -Id $editorPid -ErrorAction SilentlyContinue } else { $null }
    if ($proc) {
        Write-Host "== closing Unity (pid $editorPid)"
        unity command eval --no-banner --project-path $src --timeout 20 `
            --code 'UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.EditorApplication.update += () => UnityEditor.EditorApplication.Exit(0); return "exiting";' 2>$null | Out-Null
        if (-not $proc.WaitForExit(60000)) {
            Write-Host '   still running, stopping the process'
            Stop-Process -Id $editorPid -Force
            $proc.WaitForExit(20000) | Out-Null
        }
        Start-Sleep -Seconds 2
    }

    Write-Host "== git worktree remove $dir"
    git -C $main worktree remove --force $dir
    if ($LASTEXITCODE -ne 0) { throw "git worktree remove failed (a process may still hold files in $dir)" }
}
git -C $main worktree prune

if ($DeleteBranch -and (git -C $main branch --list $branch)) {
    if ($Force) { git -C $main branch -D $branch }
    else {
        git -C $main branch -d $branch
        if ($LASTEXITCODE -ne 0) { throw "branch $branch is not merged; merge it or pass -Force" }
    }
}
if ((Test-Path $parent) -and -not (Get-ChildItem $parent -Force)) { Remove-Item $parent }
Write-Host "removed $Name"
