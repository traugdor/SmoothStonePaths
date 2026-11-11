using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SmoothStonePaths;
using Xunit;

namespace SmoothStonePaths.Tests;

public class TargetRulesTests
{
    private const string Box = "{x1:0,y1:0,z1:0,x2:1,y2:0.9375,z2:1}";

    private static TargetDecision Classify(string json, string domain = "game", TargetOptions o = null, params string[] pathKeys)
    {
        return TargetRules.ClassifyReceiver(JObject.Parse(json), domain, o ?? new TargetOptions(), new HashSet<string>(pathKeys));
    }

    private static TargetOptions Blending() => new TargetOptions { TerrainBlending = true };

    [Fact]
    public void VanillaStonePath_IsPath() => Assert.Equal(TargetKind.Path, Classify("{code:'stonepath', collisionbox:" + Box + "}").Kind);

    [Fact]
    public void LowerCaseBlockClass_IsPlain() => Assert.Equal(TargetKind.Path, Classify("{code:'stonepath', class:'block', collisionbox:" + Box + "}").Kind);

    [Fact]
    public void Stairs_IsNever() => Assert.Equal(TargetKind.None, Classify("{code:'stonepathstairs', collisionbox:" + Box + "}").Kind);

    [Fact]
    public void BlockStairsClass_IsSkipped()
    {
        TargetDecision decision = Classify("{code:'edgestone', class:'BlockStairs', collisionbox:" + Box + "}");
        Assert.Equal(TargetKind.None, decision.Kind);
        Assert.Contains("custom class", decision.Reason);
    }

    [Fact]
    public void Farmland_IsSkipped() => Assert.Equal(TargetKind.None, Classify("{code:'farmland', class:'BlockFarmland', collisionbox:" + Box + "}").Kind);

    [Fact]
    public void MixedCollisionBoxByType_IsNotPath()
    {
        string json = "{code:'stonepath', collisionboxByType:{'*-free':" + Box + ", '*-snow':{x1:0,y1:0,z1:0,x2:1,y2:0.5,z2:1}}}";
        Assert.Equal(TargetKind.None, Classify(json).Kind);
    }

    [Fact]
    public void AlreadyOurPathClass_IsPath() => Assert.Equal(TargetKind.Path, Classify("{code:'stonepath', class:'BlockBlendedPath'}").Kind);

    [Fact]
    public void Excluded_IsNone()
    {
        var options = new TargetOptions { Exclude = ["moreroads:stoneroad3"] };
        Assert.Equal(TargetKind.None, Classify("{code:'stoneroad3', collisionbox:" + Box + "}", "moreroads", options).Kind);
    }

    [Fact]
    public void PathSlab_IsSlab() => Assert.Equal(TargetKind.Slab, Classify("{code:'stonepathslab'}", "game", null, "game:stonepath").Kind);

    [Fact]
    public void OtherSlab_IsNone() => Assert.Equal(TargetKind.None, Classify("{code:'cobblestoneslab'}", "game", null, "game:stonepath").Kind);

    [Fact]
    public void IncludedSlab_IsSlab()
    {
        var options = new TargetOptions { IncludeSlabs = ["game:woodslab"] };
        Assert.Equal(TargetKind.Slab, Classify("{code:'woodslab'}", "game", options).Kind);
    }

    [Fact]
    public void Cobblestone_IsPathByInclude()
    {
        const string json = "{code:'cobblestone', shape:{base:'block/basic/cube'}, drawtype:'cube', blockmaterial:'Stone'}";
        Assert.Equal(TargetKind.None, Classify(json).Kind);
        Assert.Equal(TargetKind.Path, Classify(json, "game", new TargetOptions { IncludePaths = ["game:cobblestone"] }).Kind);
    }

    [Fact]
    public void Sand_IsTerrainOnlyWhenBlending()
    {
        const string json = "{code:'sand', blockmaterial:'Sand'}";
        Assert.Equal(TargetKind.None, Classify(json).Kind);
        Assert.Equal(TargetKind.Terrain, Classify(json, "game", Blending()).Kind);
    }

    [Fact]
    public void SoilWithGrassCoverage_IsSoil()
    {
        const string json = "{code:'soil', class:'BlockSoil', blockmaterial:'Soil', variantgroups:[{loadFromProperties:'abstract/fertility'},{loadFromProperties:'abstract/grasscoverage'}]}";
        Assert.Equal(TargetKind.Soil, Classify(json, "game", Blending()).Kind);
    }

    [Fact]
    public void SoilClassWithoutGrassCoverage_IsNotAReceiver()
    {
        const string json = "{code:'cob', class:'BlockSoil', blockmaterial:'Soil', shape:{base:'block/basic/cube'}}";
        Assert.Equal(TargetKind.None, Classify(json, "game", Blending()).Kind);
    }

    [Fact]
    public void Rock_IsTerrainOnlyByInclude()
    {
        const string json = "{code:'rock', blockmaterial:'Stone', shape:{base:'block/basic/cube'}}";
        Assert.Equal(TargetKind.None, Classify(json, "game", Blending()).Kind);
        var options = new TargetOptions { TerrainBlending = true, IncludeTerrain = ["game:rock"] };
        Assert.Equal(TargetKind.Terrain, Classify(json, "game", options).Kind);
    }

    [Fact]
    public void CustomShape_IsNotTerrain()
        => Assert.Equal(TargetKind.None, Classify("{code:'mound', blockmaterial:'Soil', shape:{base:'block/termite/mound'}}", "game", Blending()).Kind);

    [Fact]
    public void MissingCode_IsNone() => Assert.Equal(TargetKind.None, Classify("{}").Kind);
}
