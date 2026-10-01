# Localization (custom; Unity Localization is not used)

Code: `Core/Code/Localization/`. Editor tooling: `Core/Code/Editor/Localization/`.

## Tables

- One `LocalizationTable` asset per domain with its own strings: `Domains/<Name>/<Name>Text.asset` (create via menu `Core/Localization Table`), `TableName` = `<Name>`. Domains without their own strings have none.
- Strings used by several domains live in the Shared table `Shared/UI/Localization/SharedText.asset` (keys in `Shared/UI/Code/Localization/SharedText.g.cs`), registered by the root from `CoreConfig`.
- The domain scope registers its table: `[SerializeField] private LocalizationTable _text = null!;` + `builder.RegisterLocalizationTable(_text);` in `ConfigureDomain`. It is added before any `Start` and removed with the scope.
- Keys are lower snake_case; table names PascalCase. Duplicate table names throw.

## Keys

- `TextKeyGenerator` writes `<TableName>Text.g.cs` into the nearest `Code/` folder (`Domains/<Name>/<Name>Text.asset` → `Domains/<Name>/Code/<Name>Text.g.cs`) on table save, move or delete, or via menu `Tools/Localization/Generate Text Keys`.
- Never edit a `.g.cs` by hand. After changing a table in the Editor, make sure the generated file changed too and recompile.
- Use keys as constants: `<Name>Text.YouWon`, `SharedText.Back`. A typo is a compile error.

## Using text

- Static label: a `LocalizedLabel` component next to the `TMP_Text`, key picked in the inspector (dropdown). The per-domain `LocalizedLabelBinder` (registered by `DomainRunner`) fills every label in the scope scene and in content scenes loaded through `DomainSceneSet`, and refreshes them on language change. No code needed.
- Dynamic text: the presenter injects `ILocalizationService`, calls `Get(key)` or `Format(key, args)`, and re-applies when `Current` changes:
  `_localization.Current.Subscribe(_ => _view.SetTitle(_localization.Get(<Name>Text.Title))).AddTo(ref _subscriptions);`
- Missing keys return `"{table}/{key}"` and log one Warn; empty translations fall back to the default language.
- Only `SettingsService.Apply` changes the language.

## Adding a language

New value in `Core/Code/Localization/Language.cs`, a column field on `LocalizationEntry`, the switch arms in `LocalizationEntry.GetText` and `Core/Code/Localization/LanguageExtensions.cs`, the language in `CoreConfig`'s supported list, and its characters in the static TMP font atlas under `Shared/UI/Fonts/`. `Core/Tests/Localization/LanguageTests.cs` fails until every arm exists.
