# Performance

## How to read these costs

Counts below describe shader texture fetches in this implementation, not a
guaranteed GPU timing. Cache behavior, texture resolution, screen coverage,
branch coherence, GPU architecture, shadows, overdraw, and URP settings matter.

Profile on the target Windows hardware with Unity GPU Profiler, RenderDoc, and
representative camera distances. Compare identical captures while changing one
module at a time.

## Terrain surface sampling

Top-layer selection reads three control textures for 1–12 layers, four for
13–16, and five for 17–20.

Regular planar surface sampling reads three texture-array values per selected
layer: albedo/height, normal/surface, and metallic.

| Quality | Base selected-layer array fetches per pixel |
| --- | ---: |
| Top2 | 6 |
| Top3 | 9 |
| Top4 | 12 |

These counts exclude control maps, lighting, shadows, global textures, and
anti-tiling modules.

### Multipliers and additions

- **Stochastic sampling** evaluates three transformed texture sets. It changes
  the selected-layer array cost from 3 to 9 fetches per layer.
- **Distance resampling** evaluates a second full texture set while active. It
  can double the regular or stochastic set cost over its fade range.
- **Triplanar** evaluates X, Y, and Z projections. It approximately triples the
  layer's projection work; use it only where needed.
- **Detail noise**, **macro noise**, and **normal noise** add one 2D sample each
  per active projection and selected layer when their branches are active.
- **Global tint** adds one world-space sample when tint or distance replacement
  is active. **Global normal** adds another.

Combinations multiply. For example, stochastic + distance resampling +
triplanar is intentionally an expensive quality mode.

## Mesh blending

Mesh blending evaluates the mesh material plus terrain intersection data and the
Terrain Surface sampling path. It can inspect up to four overlapping terrain
tiles. Cost scales with the on-screen pixels of blend meshes, so use compact
intersection meshes and avoid transparent overdraw.

## Editor-only operations

Composite, Detail, Tree, Relative Height, and procedural baking add no runtime
code path to a player build.

Authoring cost scales approximately as follows:

- Composite: affected alphamap texels × preset entries.
- Detail: affected detail-map texels × enabled detail entries.
- Relative Height: affected heightmap texels × active terrain tiles, plus preview
  resolution.
- Tree: generated candidates plus one initial spacing-index build over existing
  trees per Terrain per stroke.
- Procedural bake: output texels × tile count × enabled rule features. Cavity
  rules add neighboring height samples; region masks and multi-octave noise add
  their corresponding work.

## Measurement procedure

1. Capture a representative camera path and fixed render resolution.
2. Record GPU frame time, shader passes, and texture samples with Top2 and all
   optional modules disabled.
3. Enable one feature and recapture.
4. Repeat at near, transition, and far distances.
5. Test terrain and blend meshes separately.
6. Treat editor Scene view timings as authoring data, not player performance.

No FPS claims are made because they would not transfer reliably between
projects, scenes, and GPUs.
