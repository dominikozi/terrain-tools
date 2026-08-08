# Architecture

## Package boundary

`Runtime` contains the Terrain Surface profile, group, material binder, mesh
blend component, shader IDs, shaders, and bundled noise textures.

`Editor` contains all asset generation, validation, procedural evaluation,
backup codecs, painter windows, painter settings, stroke transactions, prototype
mapping, boundary naturalization, and Scene view preview code.

`Tests/Editor` validates the editor workflows and runtime binding behavior.

## Asset ownership

`TerrainToolsAssetLocator` is the only authority for package asset paths.
`TerrainToolsPaths` is the only authority for generated project paths. Package
assets resolve through `Packages/com.dominikozi.terrain-tools`; generated assets
resolve through `Assets/Generated/TerrainTools`.

The source repository contains no generated profile, material, backup,
alphamap, texture-array, or game TerrainData.

## Painter separation

Painter windows own UI and Scene view input. Presets own serialized intent.
Prototype resolution maps stable assets to the current TerrainData. Paint
utilities/services modify native terrain data. `TerrainPaintUndoTransaction`
groups and rolls back strokes. Relative Height additionally separates settings,
shape evaluation, heightmap writes, and preview source.

Tree and Detail deliberately have no index compatibility path. This prevents a
preset from silently painting the wrong prototype after TerrainData reordering.

`TerrainDataTransferWindow` owns source and target selection.
`TerrainDataTransferService` copies authoritative prototype lists and remaps
compatible target painting by stable asset reference. The service never copies
the source tile's painted layout to another tile.

## Boundary naturalization

The Terrain Boundary Naturalizer is an editor-only alphamap workflow. Its
window owns Scene view input and persistent project settings. The service
validates the Terrain Surface Group, captures padded alphamap snapshots, and
applies cropped results as one Undo operation. Detection, topology cleanup,
stroke masking, terrain coordinate conversion, and weight processing remain
separate internal types.

World-space sampling reads adjacent tiles through the snapshot sampler, which
keeps displacement and island generation continuous at terrain seams. The tool
does not modify heightmaps, detail maps, tree instances, Terrain Layers, or
runtime rendering state.

## Render binding

The Terrain Surface Group binds profile arrays, layer parameters, control maps,
and optional module data through material property blocks before rendering.
Capacity buckets are 12, 16, and 20 layers. A group that exceeds 20 layers uses
its configured fallback behavior instead of compiling an unbounded shader path.
