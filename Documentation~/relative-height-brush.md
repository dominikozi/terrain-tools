# Relative Height Brush

## Effect

Relative Height Brush writes target heights relative to the sampled world
height. It supports six shapes:

- Circle;
- Square;
- linear Slope;
- animation-curve Slope;
- repeated Field Furrows;
- Single Furrow.

## Setup

1. Select a Terrain.
2. Open **Tools > Terrain Tools > Painters > Relative Height Brush**.
3. Choose a shape and configure its size, height/depth, rotation, strength, and
   edge blend.
4. Enable **Paint Across Active Terrains** for seamless work over adjacent
   active tiles.
5. Enable **Lock Reference Height During Stroke** when a drag should retain its
   initial height and field-pattern origin.
6. Paint with left mouse.

![Square Relative Height Brush with its live target-height grid](Images/relative-height-brush-1.png)

*The green grid previews the target surface in Scene view without modifying
TerrainData.*

## Shape notes

Slope can use the lower edge's sampled world height as its reference. Curve
Slope maps a normalized animation curve between lower and higher offsets. Field
Furrows repeat a flat length plus trough width within the field bounds. Single
Furrow creates one oriented trough with independent length, width, depth, and
feather.

The live preview draws the footprint, slope/furrow direction, and sampled target
height grid. It never modifies TerrainData.

![Curve Slope brush with editable curve, direction, and live height preview](Images/relative-height-brush-2.png)

*Curve Slope maps the configured animation curve between the lower and higher
height offsets, with rotation controlling its world-space direction.*

## Heightmap updates and Undo

During drag, affected rectangles use `SetHeightsDelayLOD`. On mouse-up, each
modified TerrainData receives one `SyncHeightmap` call. This avoids rebuilding
Terrain LOD for every dab. The complete stroke is one Undo operation; exceptions
revert the stroke.

## Cost

There is no player runtime cost. Authoring cost grows with heightmap texels under
the rotated shape, number of active tiles, and live-preview resolution.
