---
name: architecture-map
description: Orientation map of this Unity project's architecture - layers (Core, Shared, Bootstrap, Domains), assembly reference rules, the VContainer scope tree, how the root boots and how DomainRunner runs a domain, where new code and assets belong, and which project skill to load next. Use before planning, implementing or reviewing any change under src/Assets/_Project, when deciding whether something belongs in Core, Shared, Bootstrap or a domain, or when navigating unfamiliar code.
---

# Architecture map

Paths in this skill are relative to `src/Assets/_Project/` unless they start with `src/`, `docs/` or `.claude/`.

Source of truth, in order: the code, `docs/Rules.md` (binding rules), `docs/Architecture.md` (explains the model). This skill is a map, not a rulebook: it never overrides them.

## Layers

| Layer | Folder | Role | Start reading at |
|---|---|---|---|
| Core | `Core/Code/` | App-lifetime infrastructure: domain runner, content, save, settings, input, time, audio, localization, logging, results. References nothing first-party. | `Core/Code/CoreInstaller.cs` |
| Shared | `Shared/UI/Code/`, `Shared/TestUtils/` | Reusable view widgets (no scope, no presenters); test helpers. Reference only Core. | `Shared/UI/Code/SelectorView.cs` |
| Bootstrap | `Bootstrap/Code/` | Composition root: root scope, boot modes, the boot sequence, `GameModule`/`IMainFlow` hook. References Core, Shared and leaf domains, never a main domain. | `Bootstrap/Code/RootLifetimeScope.cs` |
| Game module | e.g. `Sample/Code/` (if present) | The game: a `GameModule` asset on the root prefab registering main domains and one `IMainFlow`. References Bootstrap and domains; nothing references it. | `Bootstrap/Code/GameModule.cs` |
| Domains | `Domains/<Name>/` | Features with their own lifetime (start, end, UI/scenes/state in between). Main, sub or leaf. | any `Domains/*/Code/*Domain.cs` |

## Runtime model in six lines

1. VContainer instantiates the root prefab `Bootstrap/Prefabs/RootLifetimeScope.prefab` (via `Bootstrap/Settings/VContainerSettings.asset`) before the first scene. `Bootstrap/Scenes/Bootstrap.unity` is the only Build Settings scene.
2. Root `Configure` registers `ScopeRef(root, 0)`, calls `CoreInstaller.Install`, registers domain entry classes (`RegisterDomain<T>`), installs the game's `GameModule` (main domains + `IMainFlow`), and a flow entry point chosen by `Bootstrap/Code/BootMode.cs`.
3. `Bootstrap/Code/GameFlow.cs` awaits `CoreStartup.RunAsync` (settings, save slot), then the game's `IMainFlow`, which launches domains: `await domain.RunAsync(args, transition, ct)` returns a named OneOf union.
4. `Core/Code/Domains/DomainRunner.cs` loads the domain's scope scene additively, builds its `DomainLifetimeScope` as a child of the launcher's scope, and awaits `DomainCompletion<TResult>`.
5. Inside the scope: plain C# presenters/services (entry points) drive passive MonoBehaviour views (MVP, R3 outputs).
6. On completion, failure or cancellation the runner disposes the scope and unloads its scenes. Depth: root (0) → main (1) → sub/leaf (2), enforced.

## Where does it go?

- Has its own start and end, owns UI/scenes/state → new domain (`new-domain`).
- Logic, UI or input inside an existing domain → `domain-feature`, views and text → `ui-views`.
- App-lifetime service used by several domains, or an adapter over a Unity/IO API → Core (`core-service`).
- A widget reused by several domains' views → `Shared/UI/Code/` (`ui-views`).
- Order of domains, what launches what, transitions, parallel domains → `launching-domains`.
- Content scenes, heavy assets, content builds → `scenes-and-content`.
- Save data, user settings, configs → `persisted-data`.
- Tests of any of the above → `writing-tests`.

## Navigating

- Use the LSP tool (csharp-ls) when available: `workspaceSymbol` to find a type, `goToDefinition`, `findReferences` (who registers or consumes a service), `goToImplementation` (who implements a Core contract), `documentSymbol` for a file outline. Fall back to Grep/Glob.
- Delegate broad reading (more than a few files) to a sub-agent and keep only its conclusion.
- The sample domains that ship with the template are examples and may have been removed. `examples.md` lists which file shows which pattern, if present.
- How to compile, run tests and author assets through the Editor: `docs/UnityCli.md`.
