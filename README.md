<p align="center">
  <img src="SmoothStonePaths/modicon.png" alt="Smooth Stone Paths icon" width="96">
</p>

<h1 align="center">Smooth Stone Paths</h1>

<p align="center">
  A Vintage Story mod that blends the edges of stone paths into the grass, sand, gravel and dirt around them.
</p>

<p align="center">
  <img src="modimg.png" alt="Smooth Stone Paths title banner: a character sitting beside a stone path whose edge blends into the ground">
</p>

## What it does

Vanilla stone paths end in a hard, straight edge against the ground beside them. Smooth Stone Paths lets the neighbouring ground creep a little way onto the path, so a path through a meadow looks worn into the grass rather than carved through it.

The edge is drawn from what is actually next to each path block:

- **Grass** spreads onto the path in the same tint as the grass around it, following the local climate and season.
- **Sand, gravel and dirt** use the top texture of the block actually beside the path, so granite sand looks like granite sand and each soil fertility looks like itself.
- **Forest floor, cob, packed dirt, arid packed dirt and farmland** creep onto paths too. Grassy cob gives a grass edge, and forest floor gives its soil without the grass on it.
- **Corners and seams** blend: where two different materials meet, the edge mixes them instead of switching abruptly.
- **Variation**: about one path block in four has one side where the overgrowth reaches further, and the edge speckle varies from block to block, so long paths don't look stamped.

- **Path slabs** get sand, gravel and dirt edges, as if they had spilled down onto them. Grass does not grow onto slabs.
- **Cobblestone** (plain and fanned) gets every edge, grass included, where it lies flush with the ground. Cobble skulls are left out on purpose, so the edge never trims their skull pattern; add `game:cobbleskull` to `IncludePaths` if you want them too.

Rammed earth, mud bricks, peat and clay are treated like stone and give no edge. Neither does ground that is covered by another block, such as dirt under a slab or stairs.

It works on paths you have already placed. It adds no new blocks and saves nothing to your world, so you can remove it at any time.

## Installation

1. Download the latest `smoothstonepaths_<version>.zip` from the [releases](https://mods.vintagestory.at/smoothstonepaths#tab-files).
2. Put the zip, unextracted, in your `VintagestoryData/Mods` folder or use the `1-click install` method.
3. On a server, install it on the server in the `./data/Mods` folder (NOT the folder that contains the base game .dlls). Every client that connects will be prompted to download it.

Requires Vintage Story 1.22.

## Configuration

The mod creates `ModConfig/smoothstonepaths.json` in your `VintagestoryData` folder the first time it runs:

```json
{
  "TerrainBlending": false,
  "IncludePaths": [ "game:cobblestone", "game:cobblestonefan" ],
  "IncludeSlabs": [],
  "IncludeTerrain": [ "game:rock", "game:devastatedsoil" ],
  "Exclude": [ "moreroads:stoneroad3", "game:stonepathfullblock", "game:strawbedding", "game:mudbrick", "game:creativegrass", "game:dirtygravel", "game:muddygravel", "game:sludgygravel", "game:regolith", "game:cob" ],
  "ExtraSources": [ "game:soil", "game:forestfloor", "game:cob", "game:packeddirt", "game:drypackeddirt", "game:farmland" ],
  "ExcludeSources": []
}
```

- **TerrainBlending** (off by default): also blend natural terrain into itself. Only the tops of blocks open to the sky are affected.
  - Grass grows onto the edges of sand, wavy sand, gravel, rock, bare soil (the `none` grass coverage only), bony soil, devastated soil, packed dirt, arid packed dirt, trampled earth and rammed earth.
  - Sand spills onto gravel.
  - Soil that already has grass on it is left alone.
  - Turning it on changes how these blocks are drawn, so exposed rock in particular can make chunks a little slower to build.

The other entries pick blocks by `domain:code`, where code is the block's code without its variants, such as `game:cobblestone` or `moreroads:clayroad`. `*` matches anything, and an entry without a domain means `game`.

- **IncludePaths**: blocks that get every edge, like a path. Paths are also found on their own by their height (15/16 of a block).
- **IncludeSlabs**: slabs that get sand, gravel and dirt edges. The slabs of every path are found on their own.
- **IncludeTerrain**: blocks that grass grows onto when TerrainBlending is on, besides full blocks of sand, gravel and soil.
- **Exclude**: blocks that never get an edge.
- **ExtraSources**: blocks whose top creeps onto their neighbours, besides every block of sand and gravel.
- **ExcludeSources**: blocks whose top never creeps onto their neighbours.

On a server, the server's settings apply to every player. Restart the game or server after changing them.

## Compatibility

- Other mods' paths are found on their own: any block 15/16 of a block high is treated as a path, and its slab as a path slab. This covers [Add Some Paths](https://mods.vintagestory.at/addsomepaths) and [More Roads and Paths](https://mods.vintagestory.at/moreroads) without anything mod-specific. More Roads' temporal roads are excluded by default.
- Stairs and full-block stone paths are left as they are.
- Blocks that come with their own code (another mod's custom block class) never get an edge, so their behaviour stays intact. They can still creep onto paths: add them to `ExtraSources`. Use `Exclude` and `ExcludeSources` for any block that is picked up when it should not be.
- Paths never blend into each other: a stone path next to a gravel path keeps a clean edge.

## Known limitations

- If you place or remove a block on top of the ground beside a path, the path's edge can occasionally stay out of date until that area reloads.

## Building from source

The project targets .NET 10 and needs both a Vintage Story and Visual Studio 2026 installation.

1. Load the `SmoothStonePaths.slnx` in Visual Studio.
2. Press `CTRL + SHIFT + B` to build from source.

If the build process cannot find your game files, reinstall the game or manually set your `VINTAGE_STORY` environment variable to the correct path.

To debug:

1. Right-click `SmoothStonePaths` in the Solution Explorer tab.
2. Select `Debug > Start New Instance`.

The packaged mod is written to `Releases/smoothstonepaths_<version>.zip` when you run the CakeBuild project. CakeBuild uses Cake (C# Make), a cross-platform build automation tool whose build scripts are written in C#.

The edge masks and shapes are generated by `tools/Generate-FringeAssets.ps1` (Windows PowerShell). Run it after changing the mask settings, then rebuild.

The tests are in `tests/SmoothStonePaths.Tests` and run with `dotnet test tests/SmoothStonePaths.Tests`. Some of them read the game's own blocktypes through `VINTAGE_STORY`.
