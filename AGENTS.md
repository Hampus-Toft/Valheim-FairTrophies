# Agent Instructions - FairTrophies

FairTrophies is a BepInEx 5 / Harmony plugin for Valheim that replaces vanilla's rare-drop
"bad-luck protection" counter with one that every star level shares (and, planned, persists).
`CLAUDE.md` imports this file; keep all shared guidance here.

## Project context

- Plugin `FairTrophies/` targets `netstandard2.1`; tests `FairTrophies.Tests/` (xunit) target `net8.0`.
- Solution: `FairTrophies.slnx`. No Jotunn dependency - only BepInEx, Harmony and the game assemblies.
- How vanilla rolls drops, where it runs, and the verified drop-table data: `docs/VANILLA_DROPS.md`.
  Read it before touching `Drops/`.

## Critical gotchas

1. **A Debug `dotnet build` started inside `FairTrophies/` kills and relaunches the live Steam
   Valheim** (`PostBuild`, `AutoLaunchValheim`). As an agent, build with `-c Release` or
   `-p:AutoLaunchValheim=false`. Builds from the repo root, Release builds and `dotnet test` never
   launch the game.
2. The build needs a real Valheim + BepInEx install: references resolve from `ValheimInstallDir`
   (default Steam path; override with `VALHEIM_INSTALL_DIR` or `-p:ValheimInstallDir=...`).
3. `PostBuild` copies the DLL into `<ValheimInstallDir>\BepInEx\plugins` for every configuration.
4. Exclude `.claude/worktrees/` from searches and never `git add -A`.

## Commands

- Tests: `dotnet test` (repo root).
- Build: `dotnet build FairTrophies.slnx -c Release`.
- Thunderstore/r2modman/Gale zip: `dotnet build -c Release -t:ThunderstorePack` from `FairTrophies/`
  -> `FairTrophies/bin/Thunderstore/HampusToft-FairTrophies-<version>.zip`.

## Architecture

```text
FairTrophies/
├── FairTrophiesPlugin.cs       # BepInPlugin, Harmony setup, PluginVersion; DropTableLogger debug dump
├── FairTrophiesConfig.cs       # ConfigEntry bindings
├── Log.cs                      # Log.Diag: silent unless DiagnosticLogging is on
├── Drops/
│   ├── SharedDropCounter.cs    # Pure counter logic (unit tested)
│   └── CharacterDropPatch.cs   # Harmony prefix/postfix/finalizer on CharacterDrop.GenerateDropList
└── Thunderstore/               # Pack.ps1, manifest.template.json, README.md, CHANGELOG.md, icon.png
```

- The patch removes governed drops (trophies, or every drop <= 30% with `AllRareDrops`) from
  `m_drops` before vanilla runs, restores the list in a finalizer, and decides them in the postfix.
  Everything else stays vanilla. Keep it that way rather than reimplementing the whole method.
- Counters are keyed by item prefab name (like vanilla) and measured in expected drops: rolled
  uniformly in [0, 2), each kill subtracts `m_chance * 2^(level-1)` (or `m_chance` when
  `m_levelMultiplier` is off).
- Drops are rolled on the **creature's owner** (a client in multiplayer), so the plugin must be
  installed client-side. Persistence design must account for the roller not being the killer.
- Keep decision logic in pure static/instance methods that xunit can run; Unity/Valheim types
  can't be used in tests.

## Versioning

`PluginVersion` in `FairTrophiesPlugin.cs` is the single source of truth (`Pack.ps1` reads it). Bump
it in every change under `FairTrophies/` - PATCH by default, MINOR for a user-visible feature or
config change, MAJOR for breaking changes - and add a `Thunderstore/CHANGELOG.md` entry. Never
hand-edit a version in `manifest.template.json`.

## Verification before a PR

1. `dotnet test` passes.
2. `dotnet build FairTrophies.slnx -c Release` - 0 errors, 0 warnings.
3. PR title ends with the new version, e.g. `Persist counters per character (v0.2.0)`; body has a
   summary and a checklist with unchecked in-game items for the human.
