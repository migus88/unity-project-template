# Status check for the agentic toolchain. Read-only: installs nothing.
# Usage: pwsh -File .claude/skills/ai-setup/scripts/check.ps1   (or: powershell -ExecutionPolicy Bypass -File ...)
$ErrorActionPreference = 'SilentlyContinue'
$root = (git rev-parse --show-toplevel 2>$null)
if (-not $root) { $root = (Get-Location).Path }
Set-Location $root
$script:fail = $false
function Ok($m)        { Write-Host "  OK    $m" }
function Bad($m, $h)   { Write-Host "  FIX   $m  -> $h"; $script:fail = $true }
function Warn($m, $h)  { Write-Host "  WARN  $m  -> $h" }
function Has($c)       { [bool](Get-Command $c -ErrorAction SilentlyContinue) }

Write-Host "[1] csharp-ls"
if (Has dotnet) {
  if ((dotnet --list-sdks) -match '^(1[0-9])\.') { Ok "dotnet SDK >= 10 ($((Get-Command dotnet).Source))" }
  else { Bad "dotnet SDK 10+ missing on first dotnet in PATH" "tools/csharp-ls.md#install" }
} else { Bad "dotnet not found" "tools/csharp-ls.md#install" }
$toolsDir = Join-Path $HOME '.dotnet/tools'
if (Has csharp-ls) { Ok "csharp-ls on PATH ($((csharp-ls --version | Select-Object -First 1)))" }
elseif (Test-Path (Join-Path $toolsDir 'csharp-ls*')) { Bad "csharp-ls installed but $toolsDir not on PATH" "tools/csharp-ls.md#configure" }
else { Bad "csharp-ls not installed" "tools/csharp-ls.md#install" }
$slns = @(Get-ChildItem -Path src -Filter *.sln -File)
if ($slns.Count -eq 1 -and $slns[0].Name -eq 'src.sln') { Ok "single solution src/src.sln" }
elseif ($slns.Count -eq 0) { Bad "no .sln in src/ (Unity project files not generated)" "tools/csharp-ls.md#configure" }
else { Bad "multiple .sln in src/ ($($slns.Name -join ', ')) - csharp-ls may load a stale one" "delete all but src/src.sln" }

$link = Get-Item -Force src/.claude
if (-not $link) { Bad "src/.claude missing" "tools/csharp-ls.md#configure" }
elseif ($link.LinkType -in 'SymbolicLink', 'Junction') {
  if (Test-Path (Join-Path $link.FullName 'settings.json')) { Ok "src/.claude $($link.LinkType) -> $($link.Target)" }
  else { Bad "src/.claude $($link.LinkType) is broken ($($link.Target))" "tools/csharp-ls.md#configure" }
}
elseif ($link.PSIsContainer) { Bad "src/.claude is a real directory, not a link to ../.claude" "tools/csharp-ls.md#configure" }
else { Bad "src/.claude is a plain file (git core.symlinks off when checked out)" "tools/csharp-ls.md#configure" }

Write-Host "[2] Unity CLI"
if (Has unity) {
  Ok "unity CLI $((unity --version | Select-Object -Last 1))"
  if ((unity status --json --no-banner | Out-String) -match '"state":\s*"ready"') { Ok "Editor connected (ready)" }
  else { Warn "no ready Editor connected" "open src/ in Unity if you need live Editor commands" }
} else { Bad "unity CLI not found" "tools/unity-cli.md#install" }

Write-Host "[plugins]"
if (Has claude) {
  $plist = (claude plugin list | Out-String)
  foreach ($p in 'csharp-lsp@claude-plugins-official', 'unity@unity-agent-plugin') {
    $i = $plist.IndexOf($p)
    if ($i -ge 0 -and $plist.Substring($i, [Math]::Min(200, $plist.Length - $i)) -match 'enabled') { Ok "$p enabled" }
    else { Bad "$p not installed/enabled" "claude plugin install $p --scope project" }
  }
} else { Warn "claude CLI not on PATH" "cannot check plugins" }

Write-Host ""
if (-not $script:fail) { Write-Host "All required tools OK."; exit 0 } else { Write-Host "Some items need fixing (see FIX lines)."; exit 1 }
