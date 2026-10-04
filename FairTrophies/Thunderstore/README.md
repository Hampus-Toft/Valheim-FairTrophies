# FairTrophies

Valheim already has hidden "bad-luck protection" for rare drops (30% chance or lower): a countdown that
guarantees the drop within roughly twice the expected number of kills. FairTrophies makes that countdown fair.

## What it changes

- **Star levels no longer reset your progress.** Vanilla throws the countdown away and re-rolls it whenever you
  kill a creature whose star level changes the drop chance. FairTrophies keeps one countdown per item; a 1-star
  kill simply counts double and a 2-star kill four times, matching vanilla's intended chances. Among trophies only
  the Unbjorn's chance scales with stars; turn on `AllRareDrops` to cover the other affected drops too (Jotun
  Warrior armor molds, Deep North gemstones, Memorial Coal, goblin coins...).
- Every other drop is left to vanilla. The `NoPseudoDrops` world modifier is respected.

Planned: keeping the countdown across game restarts.

## Configuration

`BepInEx/config/com.hampustoft.fairtrophies.cfg`

| Setting | Default | |
|---|---|---|
| `General.Enabled` | true | Turn the mod off without uninstalling it. |
| `General.AllRareDrops` | false | Also use the shared countdown for non-trophy drops of 30% or less (armor molds, gemstones, coins...). |
| `Debug.LogDropTables` | false | Log every creature's trophy chance on world load. |
| `Debug.DiagnosticLogging` | false | Log every counted kill. |

## Multiplayer

Valheim rolls a creature's loot on the client that controls that creature, not on the server, so **every player
should install the mod**. Installing it on a dedicated server alone has no effect.
