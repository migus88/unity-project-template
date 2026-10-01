---
name: writing-tests
description: How to write and run tests in this Unity project - EditMode test assemblies per module (<X>.Tests asmdef with csc.rsp, precompiled NUnit/NSubstitute/AwesomeAssertions references, UNITY_INCLUDE_TESTS), InternalsVisibleTo, hand-constructed subjects, TestUtils helpers (union assertions BeCase<T>, FakeClock, InMemoryFileStorage, test domain scopes), async Task tests awaiting UniTask, hand-written fakes for internal interfaces, test naming and Arrange/Act/Assert, and the single PlayMode smoke test in Bootstrap. Use when adding tests for a service, model, migration, presenter or Core feature, creating a test assembly for a new domain, or running/diagnosing tests.
---

# Tests

Paths are relative to `src/Assets/_Project/`. Rules: `docs/Rules.md` (Testing) and `docs/CodingConventions.md` (Tests). Running tests: `docs/UnityCli.md`.

## Where

- EditMode: `<Module>/Tests/<X>.Tests.asmdef` + `csc.rsp` next to the tests; folders mirror `Code/` (`Core/Tests/Save/` ↔ `Core/Code/Save/`), namespace `<X>.Tests.<Folder>`.
- PlayMode: only the smoke test `Bootstrap/Tests/DebugRunnableDomainTests.cs` (with `Bootstrap/Tests/TestBootSetup.cs`). It debug-runs every root-registered domain against a temp storage folder; new root domains are covered automatically. Do not add more PlayMode tests without a reason.
- Shared helpers (fakes reused by several assemblies, MonoBehaviours needed by tests): `Shared/TestUtils/` (asmdef at its root, no `Code/` folder). Test MonoBehaviours must not be named `*LifetimeScope.cs`.

## New test assembly

- [ ] Copy `Core/Tests/Core.Tests.asmdef` and its `csc.rsp`; rename `name`/`rootNamespace` to `<X>.Tests`; references: `<X>`, what the tests use among `<X>`'s own references (`Core`, leaf domains, `MLock.Runtime`, `UniTask`, `VContainer`...), `TestUtils`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`. Keep `includePlatforms: ["Editor"]`, `overrideReferences: true`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` and the precompiled list (`nunit.framework.dll`, `NSubstitute.dll`, `Castle.Core.dll`, `System.Diagnostics.EventLog.dll`, `System.Security.Principal.Windows.dll`, `AwesomeAssertions.dll`, plus `OneOf.dll`/`R3.dll`/`Newtonsoft.Json.dll` as used).
- [ ] Add `[assembly: InternalsVisibleTo("<X>.Tests")]` in `<Module>/Code/AssemblyInfo.cs` (only towards its own test assembly).
- [ ] Runtime asmdefs never reference test DLLs.

## Writing a test

- [ ] Class `public sealed class <Subject>Tests`; methods `Method_Condition_Expected`; body marked `// Arrange`, `// Act`, `// Assert` (the only comments allowed).
- [ ] Construct the subject by hand with fakes. Resolve from a container only when the subject is registration or scope building (`Core/Tests/Domains/DomainRegistrationExtensionsTests.cs`, `DomainLifetimeScopeTests.cs`, `DomainRunnerTests.cs` with scopes from `Shared/TestUtils/`).
- [ ] Fakes: NSubstitute (`Substitute.For<IFoo>()`) for public interfaces. Internal interfaces cannot be proxied (no `InternalsVisibleTo` for DynamicProxyGenAssembly2): hand-write a small fake inside the test assembly.
- [ ] Reusable fakes: `Shared/TestUtils/FakeClock.cs` (settable `UtcNow`, `Advance`), `Shared/TestUtils/InMemoryFileStorage.cs` (files in memory, honours cancelled tokens).
- [ ] Assertions: AwesomeAssertions (`using AwesomeAssertions;`, `x.Should().Be(...)`); unions via `Shared/TestUtils/OneOfAssertionExtensions.cs`: `result.Should().BeCase<NotFound>();`, `var won = result.Should().BeCase<XResult.Won>().Which;`.
- [ ] Async: `public async Task Name()`; await UniTasks directly or `.AsTask()`; await every faulted UniTask before the test ends. Cancellation: `await act.Should().ThrowAsync<OperationCanceledException>()` with a pre-cancelled token.
- [ ] Dispose subjects and `DisposableBag`s in `[TearDown]`.
- [ ] Time-dependent logic: drive it with `FakeClock` and call `Tick()` yourself (`Core/Tests/Time/TimerServiceTests.cs`).
- [ ] Persistence: `InMemoryFileStorage` + real `JsonSerializer`; include stored JSON of every old version for migrations (`Core/Tests/Save/SaveStoreTests.cs`, `Core/Tests/Settings/SettingsServiceTests.cs`).

## What to test

Services, models, migrations, union-returning logic, `DomainRunner`-level behaviour, editor-tool logic, and presenters with real logic (introduce a view interface only for those). Thin wiring presenters are covered by the smoke test and a Play-mode check.

## Run

Refresh + recompile first, then `run_tests --mode EditMode --async_tests true` (optionally filtered to the assembly) and poll `test_status`; PlayMode for the smoke test (`docs/UnityCli.md`). Report which assemblies ran and passed.
