# Valheim-FairTrophies

FairTrophies by Hampus Toft - vanilla rare-drop rates without the resets. One bad-luck counter per character, saved
with the character and shared by every star level. Client & server BepInEx mod.

- What it does and how to install: [FairTrophies/Thunderstore/README.md](FairTrophies/Thunderstore/README.md)
- How vanilla rolls rare drops (decompiled + extracted drop tables): [docs/VANILLA_DROPS.md](docs/VANILLA_DROPS.md)
- Building, architecture and contribution rules: [AGENTS.md](AGENTS.md)

Release zip: `dotnet build -c Release -t:ThunderstorePack` in `FairTrophies/`.

## AI disclaimer

This mod was mostly made using [Claude Code](https://claude.com/claude-code), Anthropic's AI coding assistant. The
design and requirements are Hampus Toft's; Claude Code did most of the work: decompiling and analysing Valheim's drop
code, extracting the drop tables, and writing the code, tests and documentation, all under the author's direction and
review. Please report any problems in the [issue tracker](https://github.com/Hampus-Toft/Valheim-FairTrophies/issues).
