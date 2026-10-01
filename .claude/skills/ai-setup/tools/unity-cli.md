# Unity CLI and the Unity agent plugin

- **`unity@unity-agent-plugin`** (Unity-Technologies/unity-agent-plugin, enabled in
  `.claude/settings.json`): official Unity skills such as `unity:unity-cli`, `unity:ui-uitk`,
  `unity:urp-postprocessing` and `unity:unity-package-management`. Many of them drive the
  open Editor through the Unity CLI. Skills only, no MCP server.
- **Unity CLI** (`unity`): installs Editors, runs tests and builds, and controls a running
  Editor (`unity status`, `unity command ...`, `eval` for C#). It talks to the Editor through
  the `com.unity.pipeline` package, which is already in `src/Packages/manifest.json`.
  Requires Unity 6+.

## Check
```bash
unity --version                          # CLI present (Windows: Get-Command unity)
unity status --json --no-banner          # a connected Editor shows state "ready"
claude plugin list                       # unity@unity-agent-plugin ... enabled
```
Editor background settings (per user, shared by all Editors and worktree instances):
```bash
defaults read com.unity3d.UnityEditor5.x InteractionMode       # macOS: 1 = No Throttling
defaults read com.unity3d.UnityEditor5.x NSAppSleepDisabled    # macOS: 1 = App Nap off
```
```powershell
Get-ItemProperty 'HKCU:\Software\Unity Technologies\Unity Editor 5.x' |
  Select-Object 'InteractionMode_h*', 'ApplicationIdleTime_h*'   # Windows: 1 and 0
```

## Install

**Unity CLI.** The Unity Hub now bundles it. If it's missing:
- macOS / Linux: `curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash`
- Windows (PowerShell): `$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex`
- Open a new shell so `unity` is on `PATH`.

**Editor.** Use the version in `src/ProjectSettings/ProjectVersion.txt`:
`unity install <version>` (or install it through Unity Hub). Signing in and activating a
license are manual: `unity auth login`, or use Unity Hub.

**Plugin.** The marketplace and plugin are declared in `.claude/settings.json`. Accept the
folder-trust prompt once. If the plugin still isn't listed:
```bash
claude plugin marketplace add Unity-Technologies/unity-agent-plugin
claude plugin install unity@unity-agent-plugin --scope project
```

## Configure
- Open `src/` in the Editor at least once so `com.unity.pipeline` resolves.
- If more than one Editor is running, pass `--project-path <repo>/src` to `unity command`.
- Optional: `unity skill install claude-code --local` mirrors the pipeline package's deeper
  `unity-pipeline` skill into the project.
- Interaction Mode = No Throttling, so an Editor in the background keeps responding without
  ever being focused (agents never focus it; see `docs/UnityCli.md`). EditorPrefs
  `InteractionMode` = 1 (enum: 0 Default, 1 No Throttling, 2 Monitor Refresh Rate, 3 Custom)
  and `ApplicationIdleTime` = 0. Set it in Preferences > General > Interaction Mode, or:
  - in a running Editor (applies to every Editor started later):
    `unity command eval --no-banner --code 'UnityEditor.EditorPrefs.SetInt("InteractionMode", 1); UnityEditor.EditorPrefs.SetInt("ApplicationIdleTime", 0); return "ok";'`
  - macOS with all Editors closed: `defaults write com.unity3d.UnityEditor5.x InteractionMode -int 1`
    and `defaults write com.unity3d.UnityEditor5.x ApplicationIdleTime -int 0`.
  - Windows: the registry value names carry a hash suffix (`InteractionMode_h<hash>`); use the
    Preferences window or the `eval` above rather than writing the registry.
- macOS only: turn App Nap off for the Editor (bundle id `com.unity3d.UnityEditor5.x`, same
  domain as the EditorPrefs), effective for Editors started afterwards:
  `defaults write com.unity3d.UnityEditor5.x NSAppSleepDisabled -bool YES`.
  Unity can drop this key when it rewrites its prefs on quit; `check.sh` flags it, re-run the command.

## Verify
- `unity status` lists `<repo>/src` with state `ready`.
- `unity command` lists the Editor's commands.
- If `eval` / commands time out ("Main thread operation timed out"), the Editor is busy
  (compiling or importing) or blocked by a dialog. Never focus it: check the Interaction Mode and
  App Nap settings above, poll `unity status` and retry. If the project
  has compile errors, the Editor opens in Safe Mode and the pipeline can't connect. Fix the
  errors first (`unity pipeline list` confirms this).
- In Claude Code, typing `/unity:` shows the plugin's skills.
