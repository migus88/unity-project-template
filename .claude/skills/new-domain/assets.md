# Authoring a domain's assets in the Editor

Create scenes and assets through the live Editor (`unity command ...`, see `docs/UnityCli.md`), not by hand-writing YAML. List commands with `unity command --no-pager --detail compact --query <term>`; read a command's parameters before first use.

Order matters: the C# types must compile first.

1. Write the code files (and the `.meta` of `<Name>LifetimeScope.cs`), refresh, `recompile`, poll `recompile_status`, check `console --level error`. Re-read `<Name>LifetimeScope.cs` after import.
2. `create_folder` for `Domains/<Name>/Scenes` (and `Configs`, `Prefabs`, `Art` as needed).
3. `create_asset` of type `<Name>.<Name>Content` at `Domains/<Name>/<Name>Content.asset` and `<Name>.<Name>DomainDescriptor` at `Domains/<Name>/<Name>DomainDescriptor.asset`.
4. `create_scene` at `Domains/<Name>/Scenes/<Name>.unity`. Keep it additive-friendly: no camera, no `EventSystem`, no audio listener (the root prefab owns them).
5. In the scene: a root GameObject `<Name>LifetimeScope` with the component attached (`attach_script`). Leave its parent reference empty and `autoRun` on.
6. UI: place `Shared/UI/Prefabs/UICanvas.prefab` (and `Button`, `Slider`, `Selector` prefabs) as authored instances (`instantiate_prefab` is Editor authoring, which is allowed). Sorting order: domain screens 0, overlays 100 (the loading screen uses 1000). Attach the domain's views and wire their serialized fields.
7. Wire serialized fields with `set_serialized_field`: the scope's view/config/table/descriptor fields; `<Name>Content.ScopeScene` = the scene; descriptor `ContentDirectoryName` = `<Name>`, `LogTag` name = `<Name>`, `EditorContent` = the content asset.
8. `save_all`. Assign the descriptor on the launcher (root prefab for leaf domains, game module asset or the launching domain's scope component) and save that too.
9. Scenes are never added to Build Settings: only `Bootstrap/Scenes/Bootstrap.unity` is in there. Players load domain scenes from content directories (menu `Build/Content Directories`).

Checks with `get_serialized_fields` / `get_scene_hierarchy` before claiming done. A `{fileID: 0}` in a saved field means the reference was lost (for example an asset loaded before `open_scene` in the same script).
