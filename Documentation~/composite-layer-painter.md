# Composite Layer Painter

## Effect

Composite Layer Painter paints several TerrainLayer assets as one normalized
mixture. Each entry has a weight, coverage, noise scale, noise influence, and
seed. This is useful for repeatable ground families such as grass/soil/pebbles.

## Setup

1. Select one Terrain.
2. Open **Tools > Terrain Tools > Painters > Composite Layer Painter**.
3. Create or assign a Composite Layer Paint Preset.
4. Click **+** and choose a TerrainLayer from the searchable thumbnail grid.
   Clicking an existing layer selector opens the same grid again.
5. Configure brush size, strength, and falloff.
6. Paint with left mouse in Scene view.

The target stays pinned to the selected Terrain. The painter does not switch
tiles under the cursor.

The picker is populated exclusively from the selected TerrainData. Layers
already present in another preset entry remain visible but are disabled. A
missing layer in a loaded preset is clearly marked and can be replaced through
the picker; **Add Missing Layers To Terrain** remains available for deliberately
extending the TerrainData.

## Behavior

At every affected alphamap texel, enabled entry weights are modulated by their
noise masks, normalized, blended with the current weights, and normalized again.
Layers not targeted by the preset retain their relative proportions during a
partial-strength blend. Pixels outside the brush remain untouched.

One drag is one Undo operation. A stroke error restores its starting state.

## Cost

There is no player runtime cost. Authoring cost grows with the brush's alphamap
pixel area, Terrain alphamap resolution, and number of preset entries.
