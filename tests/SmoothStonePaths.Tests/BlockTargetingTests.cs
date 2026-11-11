using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace SmoothStonePaths.Tests;

public class BlockTargetingTests
{
    private const string Box = "{x1:0,y1:0,z1:0,x2:1,y2:0.9375,z2:1}";

    private static (AssetLocation, JObject) Type(string file, string json) => (new AssetLocation("game", "blocktypes/" + file + ".json"), JObject.Parse(json));

    private static TargetSummary Run(TargetOptions options, List<string> log, out List<(AssetLocation Location, JObject Json)> changed, params (AssetLocation, JObject)[] types)
        => BlockTargetingSystem.Rewrite(types, options, log.Add, out changed);

    [Fact]
    public void Rewrite_FindsSlabsThroughTheirPath()
    {
        TargetSummary summary = Run(new TargetOptions(), new List<string>(), out var changed,
            Type("stonepathslab", "{code:'stonepathslab', blockmaterial:'Gravel'}"),
            Type("stonepath", "{code:'stonepath', blockmaterial:'Gravel', collisionbox:" + Box + "}"));

        Assert.Equal(1, summary.Paths);
        Assert.Equal(1, summary.Slabs);
        Assert.Equal(0, summary.Sources);
        Assert.Equal("BlockBlendedSlab", (string)changed.Single(c => (string)c.Json["code"] == "stonepathslab").Json["class"]);
    }

    [Fact]
    public void Rewrite_ChangesOnlyTargets()
    {
        var options = new TargetOptions { TerrainBlending = true, ExtraSources = ["game:farmland"] };
        TargetSummary summary = Run(options, new List<string>(), out var changed,
            Type("stonepath", "{code:'stonepath', collisionbox:" + Box + "}"),
            Type("farmland", "{code:'farmland', class:'BlockFarmland', blockmaterial:'Soil', collisionbox:" + Box + "}"),
            Type("sand", "{code:'sand', blockmaterial:'Sand'}"));

        Assert.Equal(new TargetSummary(1, 0, 1, 0, 2, 1), summary);
        JObject farmland = changed.Single(c => (string)c.Json["code"] == "farmland").Json;
        Assert.Equal("BlockFarmland", (string)farmland["class"]);
        Assert.True((bool)farmland["attributes"][BlocktypeRewriter.SourceAttribute]);
        Assert.Equal("BlockBlendedTerrain", (string)changed.Single(c => (string)c.Json["code"] == "sand").Json["class"]);
    }

    [Fact]
    public void Rewrite_SkipsBlocktypeWithoutCode()
    {
        var log = new List<string>();
        TargetSummary summary = Run(new TargetOptions(), log, out var changed,
            Type("broken", "{collisionbox:" + Box + "}"),
            Type("stonepath", "{code:'stonepath', collisionbox:" + Box + "}"));

        Assert.Equal(1, summary.Paths);
        Assert.Single(changed);
        Assert.Single(log);
        Assert.Contains("broken", log[0]);
    }

    [Fact]
    public void Rewrite_SkipsMalformedBlocktype()
    {
        var log = new List<string>();
        var options = new TargetOptions { TerrainBlending = true };
        TargetSummary summary = Run(options, log, out var changed,
            Type("nullbox", "{code:'odd', collisionbox:{x1:0,y1:0,z1:0,x2:1,y2:null,z2:1}}"),
            Type("objectdrawtype", "{code:'oddsand', blockmaterial:'Sand', drawtype:{a:1}}"),
            Type("stonepath", "{code:'stonepath', collisionbox:" + Box + "}"));

        Assert.Equal(1, summary.Paths);
        Assert.Single(changed);
        Assert.Contains(log, line => line.Contains("nullbox"));
        Assert.Contains(log, line => line.Contains("objectdrawtype"));
    }

    [Fact]
    public void Rewrite_CountsOnlyCustomClassBlocksThatWouldHaveMatched()
    {
        var log = new List<string>();
        TargetSummary summary = Run(new TargetOptions(), log, out _,
            Type("chest", "{code:'chest', class:'BlockGenericTypedContainer'}"),
            Type("farmland", "{code:'farmland', class:'BlockFarmland', collisionbox:" + Box + "}"));

        Assert.Equal(1, summary.SkippedCustomClass);
        Assert.Single(log);
        Assert.Contains("farmland", log[0]);
    }
}
