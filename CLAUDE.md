# Unity Project Template

A reusable starting point for Unity games (Unity 6.6, URP, desktop). A game-agnostic core (Bootstrap, Core, Shared, the Loading and Settings domains) plus a removable example game (`Sample/`, `Domains/MainMenu`, `Domains/Gameplay`) that demonstrates the patterns.

## Repo layout

- `src/` — the Unity project (open this folder in Unity). Version: `src/ProjectSettings/ProjectVersion.txt`.
- `src/Assets/_Project/` — all first-party code and content: `Bootstrap/`, `Core/`, `Shared/`, `Domains/<Name>/`, `Sample/`.
- `docs/` — `Rules.md` and `CodingConventions.md` (binding, loaded below), `Architecture.md` (overview), `UnityCli.md` (CLI details).
- `.claude/skills/` — project skills (below).
- `src/.claude` — symlink to the root `.claude`, so Claude Code can be launched from the repo root or `src/` with the same settings, plugins and skills.

## Rules

@docs/Rules.md

## Coding conventions

All agents follow these whenever they write or change code. They are a how-to-write reference for code already decided on, not a guide for deciding what to build or how to approach a problem.

@docs/CodingConventions.md

## Architecture

Read `docs/Architecture.md` when you need the big picture (layers, domains, lifecycle, scopes). It explains concepts with a few illustrative examples, not an inventory of the code. For specifics, load the `architecture-map` skill and use the LSP; the nearest existing domain is the template for new work.

## Code navigation (LSP first, Grep second)

The `LSP` tool (csharp-ls on `src/src.sln`) is the default for C# symbols:
- Understand: `goToDefinition`, `hover` (signatures, Unity API docs), `documentSymbol`, `workspaceSymbol`, `goToImplementation`, `incomingCalls`/`outgoingCalls`.
- Before changing an API: `findReferences` and update every call site. Check signatures with `hover` instead of guessing.
- After editing: fix the diagnostics reported for edited `.cs` files. They do not replace a Unity compile or the tests.
- Grep/Glob for text, concepts, comments, string literals and non-C# files (`.asmdef`, `.prefab`, `.unity`, `.asset`, `.uxml`, `.md`).
- New `.cs` files and `.asmdef` changes are invisible to the LSP until Unity regenerates the project files. The first call can take 30–60 s.
- If `LSP` reports "No LSP server available for file type: .cs" or results are empty/stale, run `/ai-setup`.

## Working with Unity (Unity CLI)

Use the `unity` CLI; the `com.unity.pipeline` package exposes the Editor to it. Commands, flags and paths: `docs/UnityCli.md`. The `unity@unity-agent-plugin` plugin adds `/unity:*` skills (start with `unity-cli`).

- `unity status` first. If an Editor is open on `src/`, drive it with `unity command <name> --no-banner` (run from `src/`); batch mode cannot open a project that is already open.
- After editing C# or assets on disk: refresh, `recompile`, poll `recompile_status`, then `console --level error` must be empty.
- Tests in the live Editor: `run_tests --mode EditMode|PlayMode --async_tests true`, poll `test_status` (a synchronous run times out after 30 s). Editor closed: `unity test src --mode EditMode`.

## Skills

Project skills live in `.claude/skills/` and load by description; index in `.claude/skills/README.md`, maintenance in Rules §12. Start with `architecture-map` when unsure where code belongs. `/ai-setup` sets up or repairs the tooling (csharp-ls, Unity CLI).

## Example content

Menu `Tools/Template/Remove Example Content` (`src/Assets/_Project/Sample/Code/Editor/SampleContentRemover.cs`) deletes `Sample/`, `Domains/MainMenu` and `Domains/Gameplay`, clears the root prefab's game module and leaves a bootable, idle game. Once removed, doc and skill references to those paths are "if present" examples: build the game's own `GameModule` + `IMainFlow` (skill `launching-domains`). Agents never run it unless asked.
