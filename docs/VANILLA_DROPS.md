# How vanilla Valheim rolls rare drops

Verified 2026-10-04 against the installed game (Deep North era build): `CharacterDrop` decompiled from
`assembly_valheim.dll` with ilspycmd, drop tables read from the prefab asset bundles
(`valheim_Data/StreamingAssets/SoftRef/Bundles`) with UnityPy. Re-check both after a game update.

## The algorithm (`CharacterDrop.GenerateDropList`)

For each entry in the creature's `m_drops`:

1. `chance = m_chance`, multiplied by `2^(level-1)` **only if `m_levelMultiplier` is true**
   (level 1 = 0 stars, so x1 / x2 / x4).
2. If `chance <= 0.3` and the `NoPseudoDrops` world modifier is off, it uses the "pseudo-random"
   (bad-luck protection) path, otherwise a plain `Random.value <= chance` roll.
3. Pseudo path: `static Dictionary<string, Tuple<float,int>> s_pseudoCounter`, keyed by the **dropped
   item's prefab name** (so all skeleton variants share `TrophySkeleton`), storing `(chance, killsLeft)`.
   - If the stored `chance` differs from this kill's `chance` (exact float compare), the counter is
     **thrown away and re-rolled**: `Random.Range(0, (int)(2/chance))`. A different star level of a
     star-scaled drop does exactly this.
   - Otherwise `killsLeft - 1`; at `<= 0` the item drops and a new `Random.Range(0, (int)(2/chance + 1))`
     is stored. Mean is about `1/chance` kills.
4. Amount: `ScaleDrops(min, max)` (or plain random when `m_dontScale`), multiplied by `2^(level-1)`
   when `m_levelMultiplier`, `onePerPlayer` overrides it, capped at 100. So a 2-star Unbjorn drops 4
   trophies in vanilla; FairTrophies keeps every other amount but drops trophies as one.

`s_pseudoCounter` is never cleared or saved: it lives for the **game process**, so it survives logout
and even carries across worlds and characters, but is lost when the game closes.

## Where it runs

`Character.CheckDeath` only runs in the owner branch of `Character.CustomFixedUpdate`, and the
ragdoll path (`Ragdoll.Setup` -> `SaveLootList`) runs on the same machine. So drops are rolled by
**whichever peer owns the creature's ZDO** - in multiplayer that is a client (usually the first
player in the area), not the dedicated server, and not necessarily the player who landed the kill.
A server-only mod cannot change drop rolls.

## What the data says about stars

Of 112 trophy drop entries, 109 have `m_levelMultiplier = false`: the trophy chance is the same at
every star level, so vanilla's re-roll never triggers for them. Only `Unbjorn -> TrophyBjornUndead`
(10%) and the `TrainingDummy` scale with stars. No item has different base chances on different
creatures.

Star-scaled drops at or below 30% where the vanilla re-roll *does* bite:

| Creature | Item | Base chance |
|---|---|---|
| JotunWarrior, JotunWarriorDualWield | MoldArmor{Medium,Gold}{Chest,Helmet,Legs} | 3% |
| JotunWarrior, JotunWarriorDualWield | MemorialCoal | 20% |
| DvergerDeepNorth | AncientGemstone{Black,Green,Orange,Purple} | 10% |
| Unbjorn | TrophyBjornUndead | 10% |
| Goblin, GoblinArcher, GoblinShaman, GoblinDeepNorth | Coins | 25% |
| GoblinDeepNorth | Lingonberry | 20% |
| BlobMorkMini | OozeMork | 25% |
| Seal_Pup | SealBlubber | 10% |
| Asksvin_hatchling | AskBladder, AskHide, AsksvinMeat | 20% |
| Chicken | ChickenMeat | 25% |

Trophy chances of note: 5% Greydwarf, Neck, Surtling, Wraith, Seeker, StoneGolem, Dvergr, Charred,
Morgen, FallenValkyrie, Hare, Tick, Deathsquito; 10% most others; 15% Boar; 30% Gjall; 33% Serpent
(above the pseudo threshold - plain roll); 50% Deer, Troll, Abomination.

To re-extract the tables after a game update, load every bundle in `StreamingAssets/SoftRef/Bundles` with UnityPy
and read each MonoBehaviour whose type tree has `m_drops` entries with `m_levelMultiplier`.
