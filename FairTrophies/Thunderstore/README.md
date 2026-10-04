# FairTrophies

*By Hampus Toft.* **Client & server mod - install it on the server and on every player.**

Valheim already has hidden bad-luck protection for every creature drop of 30% or less (trophies, Jotun armor molds,
Deep North gemstones, Memorial Coal, coins...): a countdown that guarantees the drop within twice the expected number
of kills. FairTrophies keeps vanilla's drop rates exactly, and fixes what makes that countdown unfair:

| Vanilla | FairTrophies |
|---|---|
| Killing a star level whose drop chance differs (e.g. a 1-star Jotun after 0-stars) **throws the countdown away** and rolls a new one. | One countdown per item, shared by every star level. A 1-star kill counts double and a 2-star kill four times (vanilla's own star multipliers) - never a reset. |
| The countdown lives in the memory of whichever player's game is simulating the creature - your kills can use up a friend's countdown. | Every character has their own countdowns. Kills only ever count for the character credited with them. |
| Lost every time the game closes. | Saved in the character file, so it follows the character between sessions, worlds and servers. |

Drop chances and amounts always come from the game's own drop tables - nothing to configure. The one exception:
a trophy always drops as one trophy (vanilla gives a 2-star Unbjorn's trophy x4); its star bonus comes from the
counter filling faster instead. The `NoPseudoDrops`
world modifier is respected (FairTrophies stays out of the way when it is on).

## Who gets credit for a kill

1. The last player to damage the creature - also when it then dies to something else (fall, fire, drowning, another
   creature).
2. A creature no player ever hit (mob farms, traps) counts for the player whose game was simulating it - normally the
   player standing at the farm.
3. A creature the dedicated server simulated on its own counts for the nearest player.

## Multiplayer

Valheim rolls a creature's loot on the computer simulating that creature, usually a player's. The server tells that
computer which character gets the kill, that character's client updates its countdown, and the drop appears on the
corpse as usual. The server and every client must run the **same FairTrophies version**; mismatches and players without
the mod are refused with an "incompatible version" message naming both versions.

## Configuration

`BepInEx/config/HampusToft.FairTrophies.cfg` holds a single debug option:

| Setting | Default | |
|---|---|---|
| `Debug.LogCounters` | false | Log every counted kill: who was credited, the item, whether it dropped and how many kills are left. |

## AI disclaimer

This mod was mostly made using [Claude Code](https://claude.com/claude-code), Anthropic's AI coding assistant. The
design and requirements are mine; Claude Code did most of the work: decompiling and analysing Valheim's drop code,
extracting the drop tables, and writing the code, tests and documentation, all under my direction and review. Please
report any problems on [GitHub](https://github.com/Hampus-Toft/Valheim-FairTrophies/issues).
