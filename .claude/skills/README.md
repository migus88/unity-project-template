# Project skills

Each folder is a Claude Code skill: `SKILL.md` (frontmatter `name` = folder name, `description` = when to use it) plus reference files loaded on demand. Rules are in `docs/Rules.md` and `docs/CodingConventions.md`; skills describe *how* to apply them and never restate or override them.

| Skill | Use it for |
|---|---|
| `architecture-map` | Orientation: layers, scopes, boot, where code belongs, which skill next |
| `new-domain` | Creating, renaming or removing a domain (main, leaf, sub) |
| `domain-feature` | Presenters, services, models, input, locks, time, audio, configs inside a domain |
| `ui-views` | Views, canvases, Shared.UI widgets, localization |
| `launching-domains` | Game module and main flow, `RunAsync`, transitions, overlays, parallel domains |
| `scenes-and-content` | Content scenes, `Loadable<T>`, content directories, player builds |
| `persisted-data` | Save sections, settings sections, DTOs, migrations, configs |
| `core-service` | Core services, adapters, seams, the Core-contract escape hatch |
| `writing-tests` | Test assemblies, fakes, union assertions, running tests |

## Writing and maintaining skills

- Describe patterns and checklists; cite canonical files as backticked paths (relative to the repo root, to `src/Assets/_Project/`, or to the skill folder) instead of copying code. Short skeletons are allowed only where no surviving file shows the pattern, and they must match the current Core code.
- Cite Core, Shared and Bootstrap files. Sample-content files (`Domains/MainMenu`, `Domains/Gameplay`, `Sample/`, ...) may be removed: put them in `architecture-map/examples.md` or on a line that says "if present".
- Keep `SKILL.md` short (about 100 lines); move detail into sibling files.
- Validate after editing a skill and after renaming or moving any cited file or symbol:
  `.claude/skills/check-skills.sh` (macOS/Linux/Git Bash) or `pwsh .claude/skills/check-skills.ps1`. It checks frontmatter and that every cited path exists; it cannot check symbol names or described behaviour, so grep `.claude/skills` for renamed symbols too.
