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
unity command eval --no-banner --timeout 60 --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";'
unity command recompile --no-banner            # then poll:
unity command recompile_status --no-banner     # until completed / up_to_date
unity command console --no-banner --level error --tail 50
unity command console_status --no-banner       # compile-failure flag
unity command run_tests --no-banner --mode EditMode --async_tests true [--filter Core.Tests]
unity command test_status --no-banner          # poll until "completed" (sync run_tests times out after 30 s)
unity command menu --no-banner --detach --path "Build/Content Directories"
```

Other useful commands: `open_scene`, `get_scene_hierarchy`, `get_serialized_fields`, `set_serialized_field`, `create_asset`, `save_all`, `editor_play`/`editor_stop`, `capture_game_view`, `run_script`/`eval_file` for bigger editor scripts.

Notes: auto-refresh may be disabled in the Editor, so always refresh + recompile after editing files on disk. A domain reload briefly drops the connection; retry after a few seconds. `--timeout` is in seconds (default 30). With several Editors open (worktrees), pass `--project-path <project>`; skill `worktree`. Never run anything that opens a modal dialog.

## Background Editor (never focus it)

Agents never bring the Editor to the front: no `editor_focus`, no `recompile --focus true`, no `EditorWindow.Focus`/`GetWindow`/`Show` in `eval` code. The user keeps working in other apps while agents drive one or more Editors.

- What activates the Editor: in `com.unity.pipeline` only `editor_focus` and `recompile --focus true`; launching an Editor (`unity open`, Hub). Measured with a background Editor (launched with `open -g` on macOS): `eval`, `console`, `recompile`, `run_tests` (EditMode and PlayMode), `editor_play`/`editor_stop` and `capture_game_view` did not take focus.
- What keeps working unfocused: the pipeline keeps `EditorApplication.update` ticking (auto-tick, on by default; `set_autotick`), so commands, `eval`, compiles, imports and test runs proceed.
- What does not: `EditorApplication.delayCall` (and anything built on it, e.g. some menu items and build steps) only runs while the Editor is the active app; Interaction Mode and App Nap do not change this. Run long menu work with `menu --detach` and poll, and if a step waits on `delayCall`, flush it without focusing, either once or for the session (until the next domain reload):

  ```
  unity command eval --no-banner --code 'typeof(UnityEditor.EditorApplication).GetMethod("Internal_CallDelayFunctions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null); return "flushed";'
  unity command eval --no-banner --code 'var m = typeof(UnityEditor.EditorApplication).GetMethod("Internal_CallDelayFunctions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic); UnityEditor.EditorApplication.update += () => { if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive) m.Invoke(null, null); }; return "pump on";'
  ```

  `QueuePlayerLoopUpdate` and `RepaintAllViews` do not run delayed calls.
- If commands time out: the Editor is busy (import, compile, a long `eval`) or blocked by a dialog. Check Preferences > General > Interaction Mode = No Throttling (EditorPrefs `InteractionMode` = 1, `ApplicationIdleTime` = 0; checked by `/ai-setup`) and, on macOS, that App Nap is off for Unity (`NSAppSleepDisabled`). Then poll `unity status` and retry; do not focus the Editor.
- Play mode in an unfocused Editor crawls unless Player Settings > Resolution and Presentation > Run In Background is on (`runInBackground: 1` in `src/ProjectSettings/ProjectSettings.asset`; the template ships it on). An Editor that is already running keeps the old in-memory value until it restarts, or until `unity command eval --no-banner --code 'UnityEditor.PlayerSettings.runInBackground = true; return "on";'`. Side effect: desktop builds keep running when alt-tabbed.

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
