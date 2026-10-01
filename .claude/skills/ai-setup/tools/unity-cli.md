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

## Verify
- `unity status` lists `<repo>/src` with state `ready`.
- `unity command` lists the Editor's commands.
- If `eval` / commands time out ("Main thread operation timed out"), the Editor is busy
  (compiling or importing) or throttled in the background. Focus it and retry. If the project
  has compile errors, the Editor opens in Safe Mode and the pipeline can't connect. Fix the
  errors first (`unity pipeline list` confirms this).
- In Claude Code, typing `/unity:` shows the plugin's skills.
