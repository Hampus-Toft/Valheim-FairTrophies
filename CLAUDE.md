# CLAUDE.md

All shared guidance - commands, build gotchas, architecture, versioning and PR format - lives in `AGENTS.md`:

@AGENTS.md

## Claude Code specifics

- Never run a Debug `dotnet build` from inside `FairTrophies/`: it kills and relaunches the user's live Valheim.
  Use `dotnet build FairTrophies.slnx -c Release`.
- Exclude `.claude/worktrees/` from Glob/Grep, and stage files by name rather than `git add -A`.
