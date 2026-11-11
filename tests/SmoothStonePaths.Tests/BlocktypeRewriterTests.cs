using Newtonsoft.Json.Linq;
using Xunit;

namespace SmoothStonePaths.Tests;

public class BlocktypeRewriterTests
{
    private static JObject Rewrite(string json, TargetKind kind, bool source = false)
    {
        JObject blocktype = JObject.Parse(json);
        BlocktypeRewriter.Apply(blocktype, kind, source);
        return blocktype;
    }

    [Fact]
    public void Path_GetsClassAndKeepsJsonDrawtype()
    {
        JObject result = Rewrite("{code:'stonepath', drawtype:'json'}", TargetKind.Path);
        Assert.Equal("BlockBlendedPath", (string)result["class"]);
        Assert.Equal("json", (string)result["drawtype"]);
    }

    [Fact]
    public void CubeReceiver_IsSwitchedToJson()
        => Assert.Equal("Json", (string)Rewrite("{code:'cobblestone', drawtype:'cube'}", TargetKind.Path)["drawtype"]);

    [Fact]
    public void ReceiverWithoutDrawtype_IsSwitchedToJson()
        => Assert.Equal("Json", (string)Rewrite("{code:'bonysoil'}", TargetKind.Terrain)["drawtype"]);

    [Fact]
    public void Soil_SwitchesOnlyCubeByTypeEntries()
    {
        JObject result = Rewrite("{code:'soil', class:'BlockSoil', drawtypeByType:{'*-none':'Cube','*':'TopSoil'}}", TargetKind.Soil);
        Assert.Equal("BlockBlendedSoil", (string)result["class"]);
        Assert.Equal("Json", (string)result["drawtypeByType"]["*-none"]);
        Assert.Equal("TopSoil", (string)result["drawtypeByType"]["*"]);
        Assert.Null(result["drawtype"]);
    }

    [Fact]
    public void Source_GetsAttributeOnly()
    {
        JObject result = Rewrite("{code:'farmland', class:'BlockFarmland', drawtype:'json'}", TargetKind.None, true);
        Assert.True((bool)result["attributes"][BlocktypeRewriter.SourceAttribute]);
        Assert.Equal("BlockFarmland", (string)result["class"]);
        Assert.Equal("json", (string)result["drawtype"]);
    }

    [Fact]
    public void SourceKeepsOtherAttributes()
    {
        JObject result = Rewrite("{code:'sand', attributes:{mapColorCode:'land'}}", TargetKind.None, true);
        Assert.Equal("land", (string)result["attributes"]["mapColorCode"]);
        Assert.True((bool)result["attributes"][BlocktypeRewriter.SourceAttribute]);
    }

    [Fact]
    public void ApplyTwice_GivesSameJson()
    {
        const string json = "{code:'soil', class:'BlockSoil', drawtypeByType:{'*-none':'Cube','*':'TopSoil'}}";
        JObject once = Rewrite(json, TargetKind.Soil, true);
        JObject twice = (JObject)once.DeepClone();
        BlocktypeRewriter.Apply(twice, TargetKind.Soil, true);
        Assert.True(JToken.DeepEquals(once, twice));
    }

    [Fact]
    public void NothingToDo_LeavesBlocktypeUnchanged()
    {
        const string json = "{code:'rammed', drawtype:'cube'}";
        Assert.True(JToken.DeepEquals(JObject.Parse(json), Rewrite(json, TargetKind.None)));
    }
}
