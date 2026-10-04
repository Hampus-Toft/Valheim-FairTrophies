# Agent Instructions - FairTrophies

FairTrophies (author: Hampus Toft, plugin ID `HampusToft.FairTrophies`) is a BepInEx 5 / Harmony client & server
plugin for Valheim. It replaces vanilla's rare-drop "bad-luck protection" counter with per-character counters that
every star level shares, keeping vanilla's drop rates. `CLAUDE.md` imports this file; keep shared guidance here.

## Project context

- Plugin `FairTrophies/` targets `netstandard2.1`; tests `FairTrophies.Tests/` (xunit) target `net8.0`.
- Solution: `FairTrophies.slnx`. No Jotunn dependency - only BepInEx, Harmony and the game assemblies.
- How vanilla rolls drops, where it runs, and the verified drop-table data: `docs/VANILLA_DROPS.md`.
  Read it before touching `Drops/` or `Network/`.
- Product rules from the author: no config for drop rates or behaviour - chances and amounts always come from the
  game's drop tables. The only config is the `Debug.LogCounters` logging switch.

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
- Release zip for Thunderstore/r2modman/Gale: `dotnet build -c Release -t:ThunderstorePack` from `FairTrophies/`
  -> `FairTrophies/bin/Thunderstore/HampusToft-FairTrophies-<version>.zip`
  (manifest.json + icon.png + README.md + CHANGELOG.md + FairTrophies.dll).

## Architecture

```text
FairTrophies/
├── FairTrophiesPlugin.cs       # BepInPlugin (PluginVersion), PatchAll, RPC registration on each ZRoutedRpc
├── FairTrophiesConfig.cs       # Debug.LogCounters - the only setting
├── Log.cs                      # Log.Counter: silent unless LogCounters is on
├── Drops/
│   ├── SharedDropCounter.cs    # Pure counter logic + m_customData text format (unit tested)
│   ├── StarScaling.cs          # Pure: star level -> counter weight or amount multiplier, never both (unit tested)
│   ├── CharacterCounters.cs    # A character's counters in Player.m_customData; decides drops for one kill
│   ├── CharacterDropPatch.cs   # Strips governed drops from GenerateDropList and reports the kill; ragdoll context
│   └── KillAttribution.cs      # Tags creatures with the last player to damage them (ZDO key)
├── Network/
│   ├── KillRouting.cs          # Kill (owner -> server) -> Count (server -> credited client) -> Drop (-> owner)
│   └── VersionCheck.cs         # Exact-version handshake before vanilla PeerInfo; custom error text
└── Thunderstore/               # Pack.ps1, manifest.template.json, README.md, CHANGELOG.md, icon.png
```

- Governed drops: base `m_chance` <= 0.3 (vanilla's pseudo-random threshold), unless the `NoPseudoDrops` world
  modifier is on. Everything else stays in vanilla's `GenerateDropList`; keep it that way rather than
  reimplementing the method.
- Counters are keyed by item prefab name (like vanilla) and measured in expected drops: rolled uniformly in [0, 2),
  each kill subtracts `min(1, m_chance * weight)`, overshoot carries over.
- Stars multiply loot exactly once (`Drops/StarScaling.cs`, author's rule): for `m_levelMultiplier` drops, trophies,
  `AncientGemstone*` and `MoldArmor*` get weight `2^(level-1)` and amount x1; every other item gets weight 1 and
  amount x`2^(level-1)`. Vanilla applies both (up to x16).
- Credit order (server, `KillRouting.RPC_Kill`): last player to damage the creature -> the creature owner's local
  player -> nearest player. The credited player's own client updates its counters; the server stores nothing.
- Every RPC payload change is a protocol change: bump `PluginVersion` so `VersionCheck` keeps old and new apart.
- Keep decision logic in pure classes that xunit can run; Unity/Valheim types can't be used in tests.

## Versioning

`PluginVersion` in `FairTrophiesPlugin.cs` is the single source of truth (`Pack.ps1` reads it), released as git tag
`v<version>`. Bump it in every change under `FairTrophies/` - PATCH by default, MINOR for a user-visible feature,
MAJOR for breaking save-format changes - and add a `Thunderstore/CHANGELOG.md` entry. Never hand-edit a version in
`manifest.template.json`.

## Publishing (human only)

Releases go to Thunderstore and Hexium (team `HampusMods`) via `tools/Publish.ps1`, which also tags the release
`v<version>`. **Agents never run it without `-DryRun`, and never upload to either site by any other means** (direct
API calls, `tcli`, ...) - not even when asked to "release" or "publish"; hand the command to the human instead.
`.claude/settings.json` makes Claude Code ask before any command mentioning `Publish.ps1`.

The script only publishes merged code: it refuses unless `HEAD` is exactly `origin/main` with no local changes,
`Thunderstore/CHANGELOG.md` has an entry for `PluginVersion`, and the version is newer than what each site lists (a
site already on that version is skipped). It then packs, and asks the human to type the version before uploading.
`-DryRun` runs every check and the pack without uploading; `-Site Thunderstore|Hexium` limits it to one site.

API tokens live only in the user environment variables `THUNDERSTORE_TOKEN` and `HEXIUM_TOKEN`. Never write a token
into the repo, a commit, a PR, a log line, or any file.

## Verification before a PR

1. `dotnet test` passes.
2. `dotnet build FairTrophies.slnx -c Release` - 0 errors, 0 warnings.
3. PR title ends with the new version, e.g. `Fix ragdoll drops (v1.0.1)`; body has a summary and a checklist with
   unchecked in-game items for the human (two clients + dedicated server: credit, drops, version rejection).
