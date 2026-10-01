# Unity Project Template

A starting point for Unity games, built to be easy to work in for both people and AI agents.

It has two parts:

- **A game-agnostic core**: boot, dependency injection, domain lifecycle, saving, settings, input, audio, localization, content loading and a loading screen.
- **A small example game** that shows the patterns. One menu command removes it.

- **Unity** 6000.6.3f1 (Unity 6.6), URP, desktop
- **Libraries**: [VContainer](https://github.com/hadashiA/VContainer), [UniTask](https://github.com/Cysharp/UniTask), [R3](https://github.com/Cysharp/R3), [OneOf](https://github.com/mcintyre321/OneOf), Newtonsoft.Json, Input System, TextMesh Pro, [MLock](https://github.com/migus88/MLock) (input locking)
- **Tests**: NUnit, NSubstitute and AwesomeAssertions, with EditMode tests per assembly and a PlayMode smoke test

## Getting started

1. Click **Use this template** on GitHub, or clone the repo. Unity needs `git` on `PATH` to fetch MLock, which is a git-URL package.
2. Open the `src/` folder in Unity 6000.6.3f1. The repo root holds the docs and agent configuration, not the Unity project.
3. Open `Assets/_Project/Bootstrap/Scenes/Bootstrap.unity` and press Play. You can also press Play from any domain's scene; the game boots through Bootstrap first.
4. When you're ready to build your own game, run **Tools → Template → Remove Example Content**. See [Starting your own game](#starting-your-own-game).

> **Windows:** `src/.claude` is a git symlink. Enable Developer Mode and `git config --global core.symlinks true` before cloning. Otherwise it checks out as a plain text file; `/ai-setup` explains how to fix that.

## How it's organized

```
docs/                  Architecture overview, rules, coding conventions, Unity CLI notes
.claude/               Claude Code settings and project skills
src/                   The Unity project
  Assets/_Project/
    Core/              App-lifetime infrastructure (domain runner, save, settings, input, audio, ...)
    Shared/            Reusable UI widgets and test helpers
    Bootstrap/         Composition root: boots the app and hands control to the game
    Domains/           Features with their own lifetime (Loading, Settings, MainMenu, Gameplay)
    Sample/            The example game module (removable)
```

The game is a tree of **domains**. A domain is a feature with a start, an end and its own scenes, UI and state in between. Launching one works like calling an async function: pass arguments, await it, and handle the result it returns. Domains never talk to each other any other way. Dependencies point one way, towards Core, and the game plugs into Bootstrap as a **game module**, so the core never knows the game.

The full picture is in [docs/Architecture.md](docs/Architecture.md).

## Starting your own game

**Tools → Template → Remove Example Content** deletes `Sample/`, `Domains/MainMenu` and `Domains/Gameplay`. The project still compiles and boots afterwards: you get the loading screen, settings and save loading, and then an idle main flow. From there:

1. Create your domains, following the Settings domain as a model.
2. Run **Tools → Foundation → Create Game Module**. It creates `Assets/_Project/<Name>/` with a `GameModule` asset, an `IMainFlow` stub, a variant of the `RootLifetimeScope` prefab and a `VContainerSettings` asset, and makes that the game that boots.
3. Register your domains in the game module and run them from the main flow.

## Working with AI agents

The repo is set up for [Claude Code](https://claude.com/claude-code). You can launch it from the repo root or from `src/`.

- **[CLAUDE.md](CLAUDE.md)** loads [docs/Rules.md](docs/Rules.md) and [docs/CodingConventions.md](docs/CodingConventions.md) into every session and covers how to work in the repo.
- **Project skills** in `.claude/skills/` cover the recurring tasks: a new domain, a feature inside a domain, UI, scenes and content, saved data, Core services and tests. They load on demand, and `check-skills.sh` keeps their file references from going stale.
- **Code navigation** goes through the LSP tool (csharp-ls on `src/src.sln`), with grep for text and concepts.
- **The Unity Editor** is driven through the [Unity CLI](docs/UnityCli.md): refresh, recompile, read the console and run tests.
- **`/ai-setup`** installs and checks the tooling on macOS, Windows or Linux: csharp-ls and the Unity CLI and plugin. Run it after cloning.

## Docs

| Document | What's in it |
|---|---|
| [Architecture](docs/Architecture.md) | The ideas: layers, domains, lifecycle, scopes |
| [Rules](docs/Rules.md) | Binding rules: boundaries, DI, async, data, Unity hygiene, agent workflow |
| [Coding Conventions](docs/CodingConventions.md) | How code is written once you've decided to write it |
| [Unity CLI](docs/UnityCli.md) | Driving the Editor from the terminal |
