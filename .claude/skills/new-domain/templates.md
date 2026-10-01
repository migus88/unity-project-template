# Domain code skeletons

Placeholders: `<Name>` (PascalCase domain name), `<Main>` (parent of a sub-domain). The shapes mirror `Core/Code/Domains/` (`DomainRunner`, `DomainDescriptor`, `DomainContent`, `DomainLifetimeScope`, `IDebugRunnableDomain`, `DomainRegistrationExtensions`); if these skeletons ever disagree with that code, the code wins and this file must be fixed.

Main or leaf domain: namespace `<Name>`. Sub-domain: namespace `<Main>.<Sub>`, every type `internal`, no `IDebugRunnableDomain`, no `Descriptor` property, no `RunDebugAsync`, no `CreateDebug`.

## `<Name>Domain.cs` (public entry class)

```csharp
using System.Threading;
using Core.Domains;
using Cysharp.Threading.Tasks;

namespace <Name>
{
    public sealed class <Name>Domain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly <Name>DomainDescriptor _descriptor;

        public <Name>Domain(DomainRunner runner, ScopeRef launcherScope, <Name>DomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

        public UniTask<<Name>Result> RunAsync(<Name>Args args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<<Name>Args, <Name>Result>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(<Name>Args.CreateDebug(), Transition.None, ct);
        }
#endif
    }
}
```

## `<Name>Args.cs` and `<Name>Result.cs` (public)

```csharp
namespace <Name>
{
    public sealed record <Name>Args
    {
#if UNITY_EDITOR
        public static <Name>Args CreateDebug() => new();
#endif
    }
}
```

```csharp
using OneOf;

namespace <Name>
{
    [GenerateOneOf]
    public sealed partial class <Name>Result : OneOfBase<<Name>Result.Done, <Name>Result.Cancelled>
    {
        public readonly record struct Done;
        public readonly record struct Cancelled;
    }
}
```

Positional args (`sealed record <Name>Args(int LevelIndex)`) are fine. A long-lived domain that only ends through cancellation still declares a result (usually one case).

## `<Name>DomainDescriptor.cs` (public) and `<Name>Content.cs` (internal)

```csharp
using Core.Domains;
using UnityEngine;

namespace <Name>
{
    [CreateAssetMenu(menuName = "Domains/<Name> Descriptor")]
    public sealed class <Name>DomainDescriptor : DomainDescriptor
    {
    }
}
```

```csharp
using Core.Domains;
using UnityEngine;

namespace <Name>
{
    [CreateAssetMenu(menuName = "Domains/<Name> Content")]
    internal sealed class <Name>Content : DomainContent
    {
    }
}
```

The descriptor ships in the player build: never put `LoadableSceneId` or `Loadable<T>` on it. Content scenes and loadable assets go on `<Name>Content` (`[field: SerializeField] public LoadableSceneId[] EnvironmentScenes { get; private set; } = [];`).

## `<Name>LifetimeScope.cs` (internal)

```csharp
using Core.Domains;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace <Name>
{
    internal sealed class <Name>LifetimeScope : DomainLifetimeScope
    {
        [SerializeField] private <Name>View _view = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<<Name>Presenter>();
        }
    }
}
```

Typical additions: `builder.RegisterLocalizationTable(_text);` (field `LocalizationTable _text`), `builder.RegisterInstance(_config);`, `builder.Register<FooService>(Lifetime.Singleton);`, `builder.RegisterDomain<LeafDomain>(_leafDescriptor);`, `builder.RegisterSubDomain<SubDomain>(_subDescriptor);`.

## `LogTags.cs` and `AssemblyInfo.cs`

```csharp
using Core.Logging;

namespace <Name>
{
    internal static class LogTags
    {
        public static readonly LogTag <Name> = new("<Name>");
    }
}
```

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("<Name>.Tests")]
```

## `<Name>.asmdef` and `csc.rsp`

```json
{
    "name": "<Name>",
    "rootNamespace": "<Name>",
    "references": [
        "Core",
        "Shared.UI",
        "R3.Unity",
        "UniTask",
        "UnityEngine.UI",
        "VContainer"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Add third-party references only when used (`Unity.TextMeshPro`, `Unity.InputSystem`, `MLock.Runtime`, `Unity.Cinemachine`). A main domain may add leaf domains; never another main domain. `csc.rsp`:

```
-langversion:12
-nullable:enable
```

## Sub-domain `<Main>.<Sub>.asmref`

```json
{
    "reference": "GUID:<guid of <Main>.asmdef, from its .meta>"
}
```
