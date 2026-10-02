# Project Rules

Binding for humans and AI agents. MUST / MUST NOT are absolute; SHOULD needs a stated reason to break. The code is the source of truth for shapes and APIs; `docs/Architecture.md` explains the model. When in doubt, copy the nearest existing domain or Core service.

## 1. Boundaries and dependencies

- Core, Shared, Bootstrap and the Loading and Settings domains form the foundation package `games.engine-room.foundation` (embedded in the template at `src/Packages/games.engine-room.foundation/`; games consume it from git). The game (game module and its domains) lives in `src/Assets/_Project/`. Package code never references game code. A package change adds a CHANGELOG entry under `Unreleased`; breaking public API, persisted data, base root prefab objects or assembly names is a major version (package README).
- Assembly references (first-party):
  - `Core` references nothing first-party. `Core.Editor` → `Core`.
  - `Shared.*` → `Core`. `TestUtils` → `Core`.
  - Leaf domain (e.g. `Settings`) → `Core`, `Shared.*` only.
  - Main domain (e.g. `Gameplay`) → `Core`, `Shared.*`, leaf domains. MUST NOT reference another main domain.
  - `Bootstrap` → `Core`, `Shared.*`, leaf domains; it knows no main domain.
  - Game module (e.g. `Sample`: a `GameModule` + `IMainFlow`) → `Bootstrap`, `Core`, `Shared.*`, main and leaf domains. Nothing else references `Bootstrap` or the game module.
  - `<X>.Tests` → `<X>`, its allowed references, `TestUtils`.
  - `<X>.Editor` → `<X>`, its allowed references, `Core.Editor` (a game module's editor code may also reference `Bootstrap.Editor`).
- Domains never talk to each other. A launcher awaits `RunAsync` and handles the returned union. No message bus, no shared static state.
- Escape hatch (rare): Core declares an interface, a domain implements it, Bootstrap registers it (or a Core `Null*` default). Example: `ILoadingScreen`.
- Create a domain only for a feature with its own lifetime (a start, an end, and UI/scenes/state in between). App-lifetime infrastructure goes into Core.
- Nesting depth is root → main → sub/leaf. Never deeper.
- `InternalsVisibleTo` only towards the assembly's own test assembly.
- Do not add packages without owner approval. Never touch `com.unity.pipeline`, `com.unity.visualscripting`, `com.unity.multiplayer.center`, `com.unity.collab-proxy`. Not used: Addressables, Unity Localization, Resources, MessagePipe, DOTween, Zenject, UniRx, FluentAssertions ≥ 8.

## 2. Layout

- Foundation modules live in the package; the game module (`<Game>/`) and game domains (`Domains/<Name>/`) in `src/Assets/_Project/`. Module scripts, asmdefs and `csc.rsp` live in `<Module>/Code/`; tests in `<Module>/Tests/`; scenes, prefabs, configs and art outside `Code/`.
- A sub-domain is one folder `Domains/<Main>/<Sub>/` with `Code/<Main>.<Sub>.asmref` into the main assembly (no asmdef, no `csc.rsp`).
- Assets used by one domain live in that domain's folder; assets used by several live in `Shared/`.
- Inside `Code/`, group by feature (`Player/`, `Hud/`), not by kind (`Views/`, `Presenters/`).
- Every first-party asmdef has a sibling `csc.rsp` (`-langversion:12`, `-nullable:enable`). No root `Assets/csc.rsp`.

## 3. Runtime objects

- Game code MUST NOT create objects at runtime: no `Instantiate`/`InstantiateAsync`, `new GameObject`, `CreatePrimitive`, `AddComponent`, spawning factories or pools, `RegisterComponentOnNewGameObject`/`RegisterComponentInNewPrefab`, or widgets that instantiate (`TMP_Dropdown`; use `Shared.UI.SelectorView`).
- Author everything in the scope scene, a content scene or the root prefab. Variable counts are fixed authored sets sized for the worst case; show/hide with `SetActive`/`enabled`; reuse an element when the set runs out.
- Exceptions: VContainer instantiating the root prefab, the generated `GameInput`, Editor tooling and tests.

## 4. DI and lifecycle (VContainer)

- Constructor injection into plain C# classes only. No `[Inject]`. MonoBehaviours are never injected into and never call `Resolve`.
- MonoBehaviours enter the container as authored instances: `[SerializeField]` on the scope + `builder.RegisterComponent(...)`.
- Presenters: `RegisterEntryPoint<T>()`. Services: `Register<T>(Lifetime.Singleton)`; add an interface only for a real seam. Configs: `RegisterInstance(_config)`.
- Forbidden in game code: `FindObjectOfType`, `GameObject.Find`, `LifetimeScope.Find`, static singletons, `IObjectResolver` outside registration code.
- A domain ends when one flow presenter calls `DomainCompletion<TResult>.Complete(...)`. Guard with `IsCompleted` where inputs can race.
- Every `IDisposable` is disposed by its owner; every R3 subscription goes into a `DisposableBag`.
- Scope scenes: `autoRun = true`, parent empty (the runner supplies it). New `*LifetimeScope.cs`: VContainer's template overwrites the file on first import; write its `.meta` first or re-check the file content after import.

## 5. Presentation (MVP)

- View = passive `MonoBehaviour`: R3 `Observable<T>` outputs, verb-named input methods, purely visual code only. No game rules, no DI. `Update` in views is discouraged; presenters drive continuous values via `ITickable`.
- Presenter = plain class (input translators are `InputHandler`s). Depends on concrete views. One presenter per view; a view is never driven by two presenters.
- R3 is glue, not flow: no observables for control flow between services/domains.
- Input: generated `GameInput` callback interfaces (no polling); map activation through `IInputService.Push`; locking through MLock `ILockService<InputLockTag>` with `using`.

## 6. Async and errors

- Async work is cancellable UniTask code on the main thread: no `Task.Run`, no threads.
- Expected failures return unions (OneOf). Bugs and broken invariants throw. Cancellation stays `OperationCanceledException`.
- `try/catch` only in edge adapters (IO, JSON, platform) and deliberate cancellation boundaries; never swallow `OperationCanceledException` by accident.
- Game code never touches `System.IO`, `JsonConvert`, `SceneManager` load/unload, `Loadable<T>.Load()`, `Resources.Load` directly: use the Core adapters.

## 7. Data

- Configs are ScriptableObjects in the domain's `Configs/`, registered into its scope, read-only at runtime.
- Persistence goes through Newtonsoft via Core's `IJsonSerializer`; never `JsonUtility`. Serialize DTOs, never live objects.
- Adding, renaming, removing or reinterpreting a persisted save/settings field MUST bump that section's `CurrentVersion` and add a migration step in the same change. Envelope changes bump `CurrentFormatVersion` and keep reading the old format.
- Player-build assets (root prefab, descriptors, `CoreConfig`, the boot scene: the game's `<Game>/Scenes/Boot.unity` or the package's `Bootstrap.unity`) MUST NOT hold `Loadable<T>`/`LoadableSceneId`; only content-directory roots (`<Name>Content.asset`) may.
- Localized strings: per-domain `<Name>Text.asset`; keys via generated `*.g.cs`. Never hand-edit a `.g.cs` (or `GameInput.cs`).

## 8. Code conventions

- How code is written (naming, layout, formatting, async/union/R3/test code shape, comments): `docs/CodingConventions.md`. Binding for all code.

## 9. Testing

- EditMode tests per assembly (`<X>.Tests`); one PlayMode smoke test (`Bootstrap.PlayModeTests`).
- New logic in services, models, migrations or non-trivial presenters ships with tests.

## 10. Unity hygiene

- Every asset and folder under `Assets/` and the foundation package has a `.meta` (git packages ignore assets without one); create, move, rename and delete them together (prefer doing it through the Editor). Never hand-write GUIDs that collide.
- Never edit `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, generated `*.csproj`/`*.sln`, or `Packages/nuget-packages/InstalledPackages/`. Never edit a package outside the embedded foundation package (`Library/PackageCache` is read-only).
- Prefer creating scenes, prefabs and assets through the Editor (Unity CLI) over hand-writing YAML. Small, targeted YAML edits are acceptable; verify them in the Editor.
- Never trigger anything that opens a modal dialog in the Editor.
- After changing C# or assets: refresh + recompile and check the console for errors before claiming done. Run the affected tests.
- Leave the Editor in Edit mode with scenes saved.

## 11. Agent workflow

- Keep the main context slim: delegate broad searches, codebase exploration and multi-file investigations to sub-agents (Explore for lookups) and keep only their conclusions. Read only the files you need: get the model from `docs/Architecture.md`, then find the code through the project skills (start with `architecture-map`) and the LSP.
- Plan before large changes; split independent work across parallel sub-agents, each with a precise, self-contained brief.
- Done means verified: compiled without errors, relevant tests pass, console clean. Report exactly what was and was not verified.
- `docs/Architecture.md` explains concepts with a few illustrative examples (module, domain and key type names, at most a couple of short snippets); it never mirrors code (no file trees, exhaustive tables or copied implementations). Keep it under ~150 lines: when adding, cut elsewhere. Update it when a concept changes (layers, dependency direction, domain kinds, lifecycle, scopes) or a cited example is renamed or removed. Code-level guidance lives in the skills (§12). Do not create other documentation files unless asked.
- Commits: only when asked; small and focused; stage explicit paths (never blindly all); `.meta` files with their assets; commit only your own work.

## 12. Skills maintenance

Skills in `.claude/skills/` teach how to apply these rules; they must never go stale.

- Skills describe patterns, checklists and pitfalls and cite canonical files by path instead of copying code (short skeletons only where no surviving file shows the pattern). They never restate or override rules; on conflict the code and this file win: fix the skill.
- Cite Core, Shared, Bootstrap, Loading and Settings files (paths relative to the foundation package resolve, as do paths relative to `src/Assets/_Project/`). Example content may be removed: cite it only in `architecture-map/examples.md` or on a line marked "if present".
- A change that alters a pattern a skill describes (registration, lifecycle, asmdef layout, domain anatomy, persistence, testing) MUST update the affected skills in the same change. On rename/move/delete, grep `.claude/skills` for the old name.
- After changing skills or cited files, run `.claude/skills/check-skills.sh` (or `pwsh .claude/skills/check-skills.ps1`); it must report 0 errors. It also checks backticked paths in `docs/Architecture.md`.
- `name` equals the folder; `description` gives concrete triggers; keep `SKILL.md` short with detail in sibling files. Add a skill only for a recurring task and list it in `.claude/skills/README.md`.
- When a skill proves wrong or incomplete during work, fix it in the same change.
