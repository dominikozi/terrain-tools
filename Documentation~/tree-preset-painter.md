# Tree Preset Painter

## Effect

Tree Preset Painter creates native Terrain `TreeInstance` data from a weighted
preset. Each entry controls probability weight, random rotation, height range,
width range, and optional width-to-height locking.

## Setup

1. Register tree prefabs as Tree Prototypes on every Terrain tile.
2. Open **Tools > Terrain Tools > Painters > Tree Preset Painter**.
3. Create a preset and click **+** to choose a prefab from the searchable
   thumbnail grid, or use **Add All Tree Prototypes From Terrain**.
4. Set trees per 100 m², minimum spacing, brush size, strength, and falloff.
5. Paint with left mouse; hold Shift to erase tree types included in the preset.

The target changes to the Terrain under the cursor.

![Tree Preset Painter opening the Terrain tree-prototype thumbnail picker](Images/tree-preset-painter-1.png)

*Adding an entry opens a visual picker sourced from the current Terrain's tree
prototypes.*

## Mapping and spacing

Every enabled prefab must resolve to exactly one prototype on the hovered
TerrainData. Missing and duplicate mappings block painting.

The picker is populated from the Terrain currently shown in the window.
Already-used prefabs and ambiguous duplicate Terrain prototypes stay visible
but cannot be selected. Clicking the visual selector in an existing entry lets
you replace it without manually locating a prefab asset.

Minimum spacing uses a world-space grid index. The index is built once for a
Terrain at the start of a stroke and updated as trees are added, rather than
being rebuilt for every dab. Erasing rebuilds the cached index before later
painting in the same stroke.

![Tree preset entries with proportions, rotation, and scale controls](Images/tree-preset-painter-2.png)

*Preset weights are normalized across enabled entries; each prefab keeps its own
rotation and scale configuration.*

## Cost

The painter has no extra player runtime component. Runtime tree cost is Unity's
normal cost for the resulting TreeInstances. Authoring time depends on candidate
density, existing tree count when a stroke cache is first built, spacing, and
brush area. A single dab is capped at 2,048 candidates.
