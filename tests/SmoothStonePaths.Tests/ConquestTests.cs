using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace SmoothStonePaths.Tests;

/// <summary>
/// The Conquest texture pack switches stone paths, sand and gravel to cube drawing for its connected textures. Cube-drawn
/// blocks never reach the fringe hook, so targeting must switch them back to shape drawing, which picks the same tiles.
/// Values below are the blocktypes as Conquest VS Edition 1.0.7's patches leave them.
/// </summary>
public class ConquestTests
{
    private const string Rocks = "@.*-(andesite|basalt|bauxite|chalk|chert|claystone|conglomerate|granite|limestone|peridotite|phyllite|sandstone|shale|slate|kimberlite|tuff)";

    private static JObject Rewritten(string file, string json)
    {
        var types = new List<(AssetLocation, JObject)> { (new AssetLocation("game", "blocktypes/" + file + ".json"), JObject.Parse(json)) };
        BlockTargetingSystem.Rewrite(types, new SmoothStonePathsConfig { TerrainBlending = true }.ToOptions(), _ => { }, out var changed);
        return changed.Single().Json;
    }

    [Fact]
    public void ConquestStonePath_IsDrawnFromItsShapeAgain()
    {
        JObject path = Rewritten("stone/generic/stonepath", @"{
            code: 'stonepath', blockmaterial: 'Gravel',
            shape: { base: 'block/basic/cube-lowered-{cover}' },
            collisionbox: { x1: 0, y1: 0, z1: 0, x2: 1, y2: 0.9375, z2: 1 },
            drawtypeByType: { 'stonepath-free': 'cube', 'stonepath-snow': 'cube' },
            texturesByType: { 'stonepath-free': { all: { base: 'game:block/stone/path/stone/1', tiles: [ { base: 'game:block/stone/path/stone/*' } ], tilesWidth: 4 } } }
        }");

        Assert.Equal("BlockBlendedPath", (string)path["class"]);
        Assert.Equal("Json", (string)path["drawtypeByType"]["stonepath-free"]);
        Assert.Equal("Json", (string)path["drawtypeByType"]["stonepath-snow"]);
    }

    [Fact]
    public void ConquestSand_IsDrawnFromItsShapeAgain()
    {
        JObject sand = Rewritten("stone/sand", @"{
            code: 'sand', blockmaterial: 'Sand',
            drawtypeByType: { '" + Rocks + @"': 'cube', '*': 'json' },
            shapeByType: { '" + Rocks + @"': { base: 'block/basic/cube' }, '*': { base: 'block/basic/cube', alternates: [ { base: 'block/basic/cube', rotateY: 90 } ] } },
            texturesByType: { '*': { all: { base: 'game:block/stone/sand/{rock}' } } }
        }");

        Assert.Equal("BlockBlendedTerrain", (string)sand["class"]);
        Assert.Equal("Json", (string)sand["drawtypeByType"][Rocks]);
        Assert.Equal("json", (string)sand["drawtypeByType"]["*"]);
        Assert.True((bool)sand["attributes"][BlocktypeRewriter.SourceAttribute]);
    }
}
