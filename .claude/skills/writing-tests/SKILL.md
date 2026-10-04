---
name: writing-tests
description: How to write and run tests in this Unity project - EditMode test assemblies per module (<X>.Tests asmdef with csc.rsp, precompiled NUnit/NSubstitute/AwesomeAssertions references, UNITY_INCLUDE_TESTS), InternalsVisibleTo, hand-constructed subjects, TestUtils helpers (union assertions BeCase<T>, FakeClock, InMemoryFileStorage, test domain scopes), async Task tests awaiting UniTask, hand-written fakes for internal interfaces, test naming and Arrange/Act/Assert, and the single PlayMode smoke test in Bootstrap. Use when adding tests for a service, model, migration, presenter or Core feature, creating a test assembly for a new domain, or running/diagnosing tests.
---

# Tests

Paths are relative to the foundation package `src/Packages/games.engine-room.foundation/` (Core, Shared, Bootstrap, Loading, Settings) or the game's `src/Assets/_Project/` (game module, game domains). Rules: `docs/Rules.md` (Testing) and `docs/CodingConventions.md` (Tests). Running tests: `docs/UnityCli.md`.

## Where

- EditMode: `<Module>/Tests/<X>.Tests.asmdef` + `csc.rsp` next to the tests; folders mirror `Code/` (`Core/Tests/Save/` ↔ `Core/Code/Save/`), namespace `<X>.Tests.<Folder>`.
- PlayMode: only the smoke test `Bootstrap/Tests/DebugRunnableDomainTests.cs` (with `Bootstrap/Tests/TestBootSetup.cs`). It debug-runs every root-registered domain against a temp storage folder; new root domains are covered automatically. Do not add more PlayMode tests without a reason.
- Shared helpers (fakes reused by several assemblies, MonoBehaviours needed by tests): `Shared/TestUtils/` (asmdef at its root, no `Code/` folder). Test MonoBehaviours must not be named `*LifetimeScope.cs`.

## New test assembly

- [ ] Copy `Core/Tests/Core.Tests.asmdef` and its `csc.rsp`; rename `name`/`rootNamespace` to `<X>.Tests`; references: `<X>`, what the tests use among `<X>`'s own references (`Core`, leaf domains, `MLock.Runtime`, `UniTask`, `VContainer`...), `TestUtils`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`. Keep `includePlatforms: ["Editor"]`, `overrideReferences: true`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` and the precompiled list (`nunit.framework.dll`, `NSubstitute.dll`, `Castle.Core.dll`, `System.Diagnostics.EventLog.dll`, `System.Security.Principal.Windows.dll`, `AwesomeAssertions.dll`, plus `OneOf.dll`/`R3.dll`/`Newtonsoft.Json.dll` as used).
- [ ] Add `[assembly: InternalsVisibleTo("<X>.Tests")]` in `<Module>/Code/AssemblyInfo.cs` (only towards its own test assembly).
- [ ] Runtime asmdefs never reference test DLLs.

## Writing a test

- [ ] Before adding a test, search the fixture and the layer below it (service or model tests for a presenter, synthetic model tests for a shipped-data test) for one that already drives the same path to the same outcome; extend that one or skip.
- [ ] Class `public sealed class <Subject>Tests`; methods `Method_Condition_Expected`; body marked `// Arrange`, `// Act`, `// Assert` (the only comments allowed).
- [ ] Construct the subject by hand with fakes. Resolve from a container only when the subject is registration or scope building (`Core/Tests/Domains/DomainRegistrationExtensionsTests.cs`, `DomainLifetimeScopeTests.cs`, `DomainRunnerTests.cs` with scopes from `Shared/TestUtils/`).
- [ ] Fakes: NSubstitute (`Substitute.For<IFoo>()`) for public interfaces. Internal interfaces cannot be proxied (no `InternalsVisibleTo` for DynamicProxyGenAssembly2): hand-write a small fake inside the test assembly.
- [ ] Reusable fakes: `Shared/TestUtils/FakeClock.cs` (settable `UtcNow`, `Advance`), `Shared/TestUtils/InMemoryFileStorage.cs` (files in memory, honours cancelled tokens).
- [ ] Assertions: AwesomeAssertions (`using AwesomeAssertions;`, `x.Should().Be(...)`); unions via `Shared/TestUtils/OneOfAssertionExtensions.cs`: `result.Should().BeCase<NotFound>();`, `var won = result.Should().BeCase<XResult.Won>().Which;`.
- [ ] Async: `public async Task Name()`; await UniTasks directly or `.AsTask()`; await every faulted UniTask before the test ends. Cancellation: `await act.Should().ThrowAsync<OperationCanceledException>()` with a pre-cancelled token.
- [ ] Dispose subjects and `DisposableBag`s in `[TearDown]`.
- [ ] Layout and float values: `width.Should().BeApproximately(expected, 0.01f)`; vectors and positions through `Vector2.Distance(a, b).Should().BeLessThan(...)`. Exact `Be` on a `Vector2`/`Rect` passes in a live Editor and fails headless on rounding.
- [ ] Shipped assets (ScriptableObjects from `AssetDatabase`, content catalogues, prefabs) are shared by the whole run and may be cached by code under test: never `Destroy` them or change their fields. Need a variant? `Object.Instantiate` a copy (allowed in tests), change the copy and destroy it in `[TearDown]`. A static cache the subject fills is reset in `[TearDown]`.
- [ ] Scene and prefab fixtures: open the scene or load the prefab once in `[OneTimeSetUp]` (close it in `[OneTimeTearDown]`), restore the state tests change in `[SetUp]`; `[TearDown]` destroys only what the test created.
- [ ] Time-dependent logic: drive it with `FakeClock` and call `Tick()` yourself (`Core/Tests/Time/TimerServiceTests.cs`).
- [ ] Persistence: `InMemoryFileStorage` + real `JsonSerializer`; include stored JSON of every old version for migrations (`Core/Tests/Save/SaveStoreTests.cs`, `Core/Tests/Settings/SettingsServiceTests.cs`).

## What to test

Services, models, migrations, union-returning logic, `DomainRunner`-level behaviour, editor-tool logic, and presenters with real logic (introduce a view interface only for those). Thin wiring presenters are covered by the smoke test and a Play-mode check. What never gets a test: `docs/Rules.md` §9.

- [ ] Test a behaviour once, at the lowest layer that owns it: a service or model for logic, a presenter only for what it maps or decides, a scene test only for what the saved scene or prefab must hold (wiring, authored layout, asset hygiene).
- [ ] List the branches, boundaries and error cases first; write one test per item. `[TestCase]` rows: one per equivalence class plus each boundary (at the limit, just past it), not several values from the same class.
- [ ] Seed sweeps (`[Range]`, loops over seeds): as many seeds as it takes to reach the branches the test protects, not as many as the runtime allows.
- [ ] A "maps without throwing / not empty" check is worth keeping only as an exhaustiveness guard over every enum value or table row (it catches a new value missing from a switch).
- [ ] Before finishing, run the new tests alone and twice in a row in the same Editor (order and leak problems show on the second run).

## Run

Refresh + recompile first, then `run_tests --mode EditMode --async_tests true` (optionally filtered to the assembly) and poll `test_status`; PlayMode for the smoke test (`docs/UnityCli.md`). Report which assemblies ran and passed.
