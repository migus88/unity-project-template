---
name: ai-setup
description: Set up or repair the tools agents need in this repo (csharp-ls for the LSP tool, Unity CLI and the Unity agent plugin) on macOS, Windows or Linux. Use when the LSP tool reports "No LSP server available for file type: .cs", when `unity` is missing or can't reach the Editor, after a fresh clone, or when the user asks to set up or check the agentic environment.
---

# AI setup

Each tool below follows the same loop: **check → install → configure → verify**. Run the
status script first, then open only the reference files for tools that are not OK.

```bash
bash .claude/skills/ai-setup/scripts/check.sh          # macOS / Linux / Git Bash
```
```powershell
pwsh -File .claude/skills/ai-setup/scripts/check.ps1   # Windows (or powershell -File ...)
```

| # | Tool | Needed for | Reference |
|---|------|-----------|-----------|
| 1 | .NET SDK 10 + `csharp-ls` + `csharp-lsp` plugin; `src/.claude` is a real symlink/junction to `../.claude` | `LSP` tool: definitions, references, diagnostics; project settings, plugins and skills when Claude Code starts in `src/` | [tools/csharp-ls.md](tools/csharp-ls.md) |
| 2 | Unity CLI + `unity` agent plugin; Editor Interaction Mode = No Throttling (and App Nap off on macOS) | Driving the live Editor (also unfocused, in the background), tests, builds | [tools/unity-cli.md](tools/unity-cli.md) |

Rules:
- Work top to bottom; later tools depend on earlier ones (csharp-ls needs the generated
  `src/src.sln`, which needs Unity to have compiled the project).
- Install commands change the user's machine. Say what you are about to install before running
  it. Never use `sudo` / elevated shells without the user's go-ahead.
- If a step can't be automated (GUI installers, Unity sign-in, restarting Claude Code), stop and
  give the user the exact manual step from the reference file.
- Plugins declared in `.claude/settings.json` load at session start: after installing a
  plugin or putting a binary on `PATH`, tell the user to **restart Claude Code** (or run
  `/reload-plugins`) from a shell where the binary is on `PATH`.
- In a game project that consumes the foundation package from git (not embedded), csharp-ls
  only sees the package after enabling *External Tools → Generate .csproj files for: Git
  packages* ([tools/csharp-ls.md](tools/csharp-ls.md), Configure).
- Finish by re-running the status script and reporting each tool as OK / fixed / needs user.

## Adding a tool

Add a row to the table, a `tools/<name>.md` with the four sections (Check, Install, Configure,
Verify — each with macOS, Windows and Linux commands where they differ), and a check block in
both `scripts/check.sh` and `scripts/check.ps1`.
