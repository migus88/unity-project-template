# Working with Unity from the command line

The Unity project is `src/`. Editor version: `src/ProjectSettings/ProjectVersion.txt` (currently `6000.6.3f1`).

## Tools

- **`unity` CLI** (Unity's official CLI, installed with Unity Hub or `curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash`). Check with `unity --version` and `unity doctor`.
- **`com.unity.pipeline`** (already in `src/Packages/manifest.json`) runs inside the Editor and exposes the commands that `unity command` calls. Do not remove or reconfigure it.
- **Claude plugin `unity@unity-agent-plugin`** (enabled in `.claude/settings.json`): skills only, shown as `/unity:<skill>`. `unity-cli` is the main one (driving the Editor, building, testing); others cover uGUI, URP, audio, physics, packages, etc. Load a skill only when the task needs it.

## Two modes

| Editor open on `src/`? | Use | Why |
|---|---|---|
| Yes (usual) | `unity command <name>` against the live Editor | A project can be open in only one Unity process; batch mode fails on the project lock. |
| No (CI, closed Editor) | `unity test`, `unity build`, `unity run`, or raw batch mode | Spawns a headless Editor. |

`unity status` lists running Editors (`ready` = connected). If the project has compile errors, the Editor opens in Safe Mode and the CLI cannot connect: read `src/Logs/` / the Editor log, fix, restart.

## Live Editor (`unity command`)

Run from `src/` (or pass `--project-path src`). Add `--no-banner`; list commands with `unity command --no-pager --detail compact` and filter with `--query <term>`. Parameters are `--name value`.

```
unity command eval --no-banner --timeout 60000 --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";'
unity command recompile --no-banner            # then poll:
unity command recompile_status --no-banner     # until completed / up_to_date
unity command console --no-banner --level error --tail 50
unity command console_status --no-banner       # compile-failure flag
unity command run_tests --no-banner --mode EditMode --async_tests true [--filter Core.Tests]
unity command test_status --no-banner          # poll until "completed" (sync run_tests times out after 30 s)
unity command menu --no-banner --detach --path "Build/Content Directories"
```

Other useful commands: `open_scene`, `get_scene_hierarchy`, `get_serialized_fields`, `set_serialized_field`, `create_asset`, `save_all`, `editor_play`/`editor_stop`, `capture_game_view`, `run_script`/`eval_file` for bigger editor scripts.

Notes: auto-refresh may be disabled in the Editor, so always refresh + recompile after editing files on disk. A domain reload briefly drops the connection; retry after a few seconds. `eval` can time out while the Editor is unfocused (`editor_focus` first). Never run anything that opens a modal dialog.

## Headless (Editor closed)

```
unity test src --mode EditMode --output Temp/editmode.xml    # exit 0 = pass, 8 = failures, 6 = did not finish
unity test src --mode PlayMode --output Temp/playmode.xml
unity build src --target StandaloneOSX -o Builds/Game.app    # build content directories first
```

Raw batch mode (same as `unity test` under the hood):

```
<Unity> -batchmode -projectPath src -runTests -testPlatform EditMode -testResults Temp/editmode.xml -logFile Temp/editmode.log
<Unity> -batchmode -projectPath src -quit -logFile - -executeMethod <Namespace.Class.Method>
```

Do not pass `-quit` with `-runTests` (the run exits by itself after writing results). `-logFile -` prints the log to stdout. A compile-only check is a batch-mode launch with `-quit`: a non-zero exit code plus `error CS` lines in the log mean compile errors.

Editor binary (`<Unity>`), installed through Unity Hub:

- macOS: `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`
- Windows: `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`
- Linux: `~/Unity/Hub/Editor/<version>/Editor/Unity`
