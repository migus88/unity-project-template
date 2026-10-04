# Coding Conventions

Binding for all agents and humans whenever they write or change C#. These conventions say HOW code is written once it has been decided that the code should exist. They do not decide WHAT to build, where it belongs or how to approach a problem: that is `docs/Rules.md`, `docs/Architecture.md` and the project skills.

## Language

- C# 12 max. Only polyfill: `IsExternalInit`. No `required`, `field`, extension members, generic attributes.
- Block-scoped namespaces only (file-scoped ones break Unity's script association). Namespace = assembly root namespace + folder path, skipping `Code` segments (`Domains/Gameplay/Code/Flow` → `Gameplay.Flow`). No company prefix.
- Nullable reference types are on: annotate what can be null; non-null fields assigned later (serialized, test setup) start as `= null!`.
- Remove unused usings. Alias to resolve clashes (`using Object = UnityEngine.Object;`).

## Naming

- PascalCase: types, methods, properties, constants, enum members. camelCase: locals, parameters. `_camelCase`: private instance fields. Constants and `static readonly` fields are PascalCase.
- Interfaces `I`-prefixed. Classes and structs are nouns; methods are verbs (`SetVolume`, `LoadAsync`); booleans ask a question (`Is/Has/Can`), including bool-returning methods (`IsNewPosition`).
- Async methods end in `Async`. Role suffixes: `View`, `Presenter`, `InputHandler`, `Service`, `Model`, `Config`, `LifetimeScope`; domain entry types `Args`, `Result`.
- Meaningful, searchable names; no abbreviations (math is fine: `x`, `dt`), no Hungarian, no snake_case or kebab-case, no special characters.
- One MonoBehaviour per file; file name = class name. Other types: one top-level type per file unless they are tiny and private to it.
- Folders that would shadow Unity types are renamed (`Cameras`, not `Camera`).

## Layout and member order

- File: usings, namespace, type.
- Members: constants, static members, properties, fields, readonly fields, constructors, public methods, private methods, `Dispose`/`OnDestroy`, finalizer, nested types. Within each group: public, protected, private.
- Explicit constructors assigning `private readonly` fields. Primary constructors only for records.

## Formatting

- Allman braces; always braces, even for one-line bodies; one statement per line.
- `if (x)` not `if(x)`; `a[i]` not `a[ i ]`; `F(a, b)` not `F( a,b )`.
- Blank line between members and between logical blocks.
- `var` when the type is obvious from the right-hand side; spell the type when it is not.

## Types and modifiers

- `sealed` by default. Access modifiers always written. Domain internals `internal`; public only the entry class, `Args`, `Result` and descriptor.
- Properties instead of public fields.
- Data carriers (args, results, DTOs, union cases) are records: `sealed record` or `readonly record struct`. Persisted DTOs: `sealed record` of primitives, value types nullable.
- No LINQ in per-frame code (`Tick`, `Update`, input callbacks firing per frame).

## Unity serialization

- `[SerializeField] private T _x = null!;` or `[field: SerializeField] public T X { get; private set; }`. No public fields.
- Unity cannot serialize records or `readonly` fields; serialized `[Serializable]` structs/classes use one of the two forms above.

## Enums

- Explicit values; `None = 0`. Singular names, no prefix/suffix.
- `[Flags]` enums are plural with bit shifts (`Melee = 1 << 0`) and may define combinations.

## Async (UniTask)

- `UniTask`/`UniTask<T>` everywhere; `Task` only in test methods.
- Every async method takes `CancellationToken ct` as its last parameter, no default value. Pass `CancellationToken.None` explicitly where nothing can cancel.
- No `async void`, `.Result`, `.GetAwaiter().GetResult()`. Fire-and-forget only as `async UniTaskVoid` + `.Forget()`.

## Unions (OneOf)

- Public APIs return named unions declared in a namespace; cases are nested; shared cases (`NotFound`, `Error`, ...) come from `Core.Results`:

  ```csharp
  [GenerateOneOf]
  public sealed partial class LoadResult : OneOfBase<LoadResult.Loaded, NotFound, Error>
  {
      public readonly record struct Loaded(int Count);
  }
  ```

- Consume with `Match`/`Switch`/`TryPickT0`. Never `AsT0` (or any `AsTn`) without a check.
- Never `using OneOf.Types;` next to `Core.Results`; alias what you need: `using Success = OneOf.Types.Success;`.

## R3

- Observable state: private `ReactiveProperty<T>` exposed as `ReadOnlyReactiveProperty<T>`. No C# `event`s, no public `Subject`s.
- At most ~3 operators per chain.
- Async handler on an observable: `.SubscribeAwait((_, ct) => DoAsync(ct).AsValueTask(), AwaitOperation.Drop)`.

## Logging

- `Log` with a `LogTag`; never `Debug.Log*`. Log where an error is handled, not where it is created.

## Comments

- None of any kind (`//`, `/* */`, `///`). Exceptions: `// Arrange`, `// Act`, `// Assert` in tests; tool-generated files. Preprocessor directives are fine.

## Tests

- NUnit + NSubstitute + AwesomeAssertions; union asserts via `TestUtils` (`result.Should().BeCase<T>()`).
- Class `<Subject>Tests` (`sealed`); method `Method_Condition_Expected`. Async tests are `async Task` and await UniTask.
- Body in `// Arrange`, `// Act`, `// Assert` blocks. Construct the subject by hand, not via a container (unless the subject is registration or scope building).
- Each test asserts the outcome its name states. A bare "does not throw" or "not empty" check is only for exhaustiveness guards over every value of an enum or table.
- Floats, vectors, rects and layout values compare with a tolerance (`BeApproximately`, a distance below an epsilon), never exact equality.
- An expensive scene or prefab load goes in `[OneTimeSetUp]`, with `[SetUp]` restoring the state tests change; `[TearDown]` destroys only what the test created, never an object loaded from an asset.
