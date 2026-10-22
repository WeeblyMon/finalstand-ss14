# Hybrisa port tooling

Source: RMC-14 PR #5518 ("Hybrisa"), merged, commit `475d739517b531dac9b013fe112526ca0a394e76`.

`manifest.csv` is the single source of truth for the tile port: it maps every RMC
prototype id to its Final Stand id, palette category, locale key, display name,
base tile and texture path. Generate the tile palette from this file rather than
from the prototypes, so the two cannot drift.

## Re-running

1. Fetch `Resources/Prototypes/_RMC14/Tiles/Hybrisa/*.yml` and
   `Resources/Textures/_RMC14/Tiles/hybrisa/*.png` from the commit above into
   `rmc/` and `dl/tiles/`.
2. `python parse_hybrisa.py` — parses the YAML, audits sprite sizes against each
   tile's `variants` count, and writes `hybrisa_parsed.json`.
3. `python emit_port.py <repo root>` — writes the tile prototypes, `tiles.ftl`,
   the renamed textures and `manifest.csv`.

## Notes

- `RMCPlanetHybrisaUndergroundWeedable` and `RMCPlanetHybrisaDirtPlating` are
  skipped: they differ from their twins only by `weedsSpreadable`, which has no
  counterpart here, so they would be exact duplicates.
- 11 textures the PR adds are referenced by no tile prototype and are not ported.
- The 10 abstract colour bases are dropped. Only the wood base carried data
  (footsteps and item drop), which `FSTileBaseWood` reproduces.
