# Changelog

## 1.0.2

- Star levels multiply a rare drop's loot exactly once (x2 for 1 star, x4 for 2 stars), instead of vanilla's
  chance-and-amount (up to x16):
  - Trophies, Ancient Gemstones and armor molds drop more often, always one at a time.
  - Resources (Memorial Coal, coins, Lingonberries, Asksvin hatchling/chicken meat, hides, Mork ooze, seal blubber)
    keep the 0-star chance and drop more per drop.

## 1.0.1

- Trophies always drop as a single trophy. Star levels still make them come sooner (a 2-star kill counts four times),
  but no longer also multiply the amount - a 2-star Unbjorn dropped 4 trophies, as it does in vanilla.

## 1.0.0

- One bad-luck counter per item, shared by every star level: starred kills count 2x/4x (vanilla's multipliers)
  instead of resetting it. Covers every creature drop of 30% or less, with vanilla's own chances and amounts.
- Counters belong to each character and are saved in the character file.
- The server credits each kill to a character (last player to damage it, else the player simulating it) and routes it
  to that player's client.
- Client and server must run the same version; mismatches are refused on connect.
