# Detail Preset Painter

## Effect

Detail Preset Painter paints weighted native Terrain detail layers as one
preset. Entries support density weight, coverage, world-space noise scale,
noise influence, and deterministic seed.

## Setup

1. Register detail prefab or texture prototypes on every Terrain tile that will
   be painted.
2. Open **Tools > Terrain Tools > Painters > Detail Preset Painter**.
3. Create a preset and click **+** to open the searchable prototype grid, or use
   **Add All Detail Prototypes From Terrain**.
4. Choose a prefab- or texture-based detail by thumbnail. Clicking the selector
   in an existing entry opens the grid again.
5. Set target density, brush size, strength, and falloff.
6. Paint with left mouse; hold Shift to erase preset layers.

The tool follows the active Terrain under the cursor.

![Detail Preset Painter with a searchable thumbnail picker populated from Terrain detail prototypes](Images/detail-preset-painter-1.png)

*The picker shows only prototypes registered on the current Terrain and makes
the stored asset reference explicit.*

## Mapping rules

Presets store source assets, not prototype indices. The source must appear
exactly once in the hovered TerrainData. Missing sources, duplicate Terrain
prototypes, duplicate enabled preset entries, or entries containing both a
prefab and texture block painting.

The picker is always built from the Terrain currently shown in the window.
Already-used sources and ambiguous duplicate prototypes are shown but disabled,
with the reason available in their tooltip.

## Density and dithering

Target density is converted to native detail counts using
`maxDetailScatterPerRes`. Fractional counts are spatially dithered with a stable
hash, avoiding a uniform rounding bias. Erase uses the same brush opacity and is
clamped at zero.

## Cost

There is no player runtime cost beyond Unity's normal Terrain details. Authoring
cost grows with affected detail-map pixels and enabled entries. Large brushes on
high detail resolutions can produce visible editor stalls.
