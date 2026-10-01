# Persisted data skeletons

Core types: `Core/Code/Save/SaveSection.cs`, `Core/Code/Settings/SettingsSection.cs`, `Core/Code/Save/ISaveStore.cs`, `Core/Code/Settings/ISettingsService.cs`. If this file disagrees with them, the code wins; fix this file.

## Save section

```csharp
using Core.Results;
using Core.Save;
using Newtonsoft.Json.Linq;
using OneOf;

namespace <Name>.Progress
{
    internal static class <Name>Save
    {
        public const int CurrentVersion = 2;
        public const int DefaultBestScore = 0;
        private const string Key = "<nameInCamelCase>";

        public static readonly SaveSection<<Name>SaveDto> Section = new(Key, CurrentVersion, Migrate);

        public static OneOf<JObject, Corrupted> Migrate(JObject data, int fromVersion)
        {
            var current = data;

            for (var version = fromVersion; version < CurrentVersion; version++)
            {
                OneOf<JObject, Corrupted> migrated = version switch
                {
                    1 => MigrateFromVersion1(current),
                    _ => new Corrupted($"<Name> save data has no migration from version {version}."),
                };

                if (!migrated.TryPickT0(out current, out var corrupted))
                {
                    return corrupted;
                }
            }

            return current;
        }

        private static OneOf<JObject, Corrupted> MigrateFromVersion1(JObject data)
        {
            if (data["highScore"] is not JValue { Value: long highScore and >= int.MinValue and <= int.MaxValue })
            {
                return new Corrupted("<Name> save data version 1 has no 'highScore' that fits an int.");
            }

            return new JObject
            {
                ["bestScore"] = (int)highScore,
            };
        }
    }

    internal sealed record <Name>SaveDto(int? BestScore);
}
```

JSON property names are camelCase (the shared serializer settings). Version 1 with no migrations: `Migrate` returns `new Corrupted($"... no migration from version {fromVersion}.")`.

## Reading in the owning service

```csharp
var read = _saves.Read(<Name>Save.Section);
var dto = read.Match(
    stored => stored,
    notFound => new <Name>SaveDto(null),
    corrupted => LogCorruptedAndDefault(corrupted));
var bestScore = dto.BestScore ?? <Name>Save.DefaultBestScore;
```

Log one Warn listing fields that were `null`. Write with `_saves.Write(<Name>Save.Section, new <Name>SaveDto(bestScore));` then `await _saves.FlushAsync(ct)` and handle the `Error` case.

## Settings section

```csharp
internal static class <Name>Settings
{
    public const int CurrentVersion = 1;
    public const float DefaultCameraDistance = 0.5f;
    private const string Key = "<nameInCamelCase>";

    public static readonly <Name>SettingsDto Default = new(CameraDistance: DefaultCameraDistance);
    public static readonly SettingsSection<<Name>SettingsDto> Section = new(Key, CurrentVersion, Migrate, Default);

    public static OneOf<JObject, Corrupted> Migrate(JObject data, int fromVersion)
    {
        return new Corrupted($"<Name> settings have no migration from version {fromVersion}.");
    }
}

internal sealed record <Name>SettingsDto(float? CameraDistance);
```

The owning service validates on read (null or out of range → default + one Warn), exposes the value as `ReadOnlyReactiveProperty<T>`, writes on change, and calls `ISettingsService.SaveAsync(ct)` only when something changed.
