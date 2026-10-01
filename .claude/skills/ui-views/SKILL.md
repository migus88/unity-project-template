---
name: ui-views
description: How to build views and UI in this Unity project - passive MonoBehaviour views with R3 Observable outputs and verb-named input methods, authored uGUI/TextMeshPro canvases in scope scenes, reusable Shared.UI widgets (SelectorView, SliderView, UICanvas/Button/Slider/Selector prefabs), showing/hiding instead of spawning, cameras and canvases, and localization (LocalizationTable assets, generated TextKey constants in *Text.g.cs, LocalizedLabel, dynamic text through ILocalizationService, adding a language). Use when creating or changing a screen, HUD, overlay, menu, widget, any MonoBehaviour view, or any player-visible text.
---

# Views, UI and text

Paths are relative to `src/Assets/_Project/`. Rules: `docs/Rules.md` (Runtime objects, Presentation, Data). Presenters that drive views: `domain-feature`. Localization details: `localization.md`.

## A view

- `internal sealed class <Thing>View : MonoBehaviour` in the domain's feature folder; file name = class name; one per file.
- Outputs: `Observable<T>` properties built from components, e.g. `public Observable<Unit> BackClicked => _backButton.OnClickAsObservable();` (R3.Unity).
- Inputs: verb methods (`SetScore(int)`, `Show()`, `SetInteractable(bool)`, `PlayAsync(CancellationToken ct)` using `destroyCancellationToken` linked with `ct`).
- Serialized references: `[SerializeField] private Button _backButton = null!;`.
- Allowed Unity messages: `Awake` (cache components, purely visual listeners), `OnValidate`, `OnDrawGizmos`. No `Update` for logic; no injection; no knowledge of presenters, services or rules.
- Physics callbacks reach presenters as observables (`OnTriggerEnterAsObservable`).
- Canonical widget shapes: `Shared/UI/Code/SelectorView.cs`, `Shared/UI/Code/SliderView.cs`. Core view: `Core/Code/Localization/LocalizedLabel.cs`.

## Checklist

- [ ] Every object the view needs is authored in the scope scene (or a content scene / root prefab) and referenced by serialized fields. No `Instantiate`, `AddComponent`, `TMP_Dropdown`; variable counts are a fixed authored set sized for the worst case, toggled with `SetActive`/`enabled`.
- [ ] A view in the scope scene is a `[SerializeField]` on `<Name>LifetimeScope` + `builder.RegisterComponent(_view)`. A view in a content scene is found by the flow presenter through the content scene's root component after load (`scenes-and-content`).
- [ ] Exactly one presenter drives the view.
- [ ] Canvases: instances of `Shared/UI/Prefabs/UICanvas.prefab`, Screen Space - Overlay; sorting order 0 for domain screens, 100 for overlays (the loading screen uses 1000). `Camera.main` only in a view's `Awake` to assign a canvas world camera.
- [ ] Buttons, sliders, selectors: instances of `Shared/UI/Prefabs/Button.prefab`, `Slider.prefab`, `Selector.prefab`.
- [ ] Player-visible text is localized (`localization.md`): static labels via `LocalizedLabel`, dynamic text via `ILocalizationService`. Fonts: the TMP default font in `Shared/UI/Fonts/` must contain every character a supported language needs.
- [ ] Authoring through the Editor (`docs/UnityCli.md`); `save_all`; check wiring with `get_serialized_fields`.

## A new Shared.UI widget

Only when at least two domains need it. Public sealed MonoBehaviour in `Shared/UI/Code/`, no scope, no presenter, references only Core (asmdef `Shared/UI/Code/Shared.UI.asmdef`). Ship a prefab in `Shared/UI/Prefabs/`.

## Verify

Refresh, recompile, empty console. Press Play in the scope scene and look (`capture_game_view`; delete saved screenshots under `Assets/` afterwards).

## Pitfalls

- `Screen Space - Camera` canvases need the root camera; the root prefab owns the only `Camera`, `EventSystem` and audio listener. Never add them to domain scenes.
- Pushing `InputMaps.Ui` does not gate uGUI clicks (the `InputSystemUIInputModule` has its own actions); disable interactables with `SetInteractable(false)` while an action runs.
