using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace SmoothStonePaths.Tests;

public class SourceRulesTests
{
    private static bool IsSource(string json, string domain = "game", TargetOptions o = null, params string[] pathKeys)
    {
        return TargetRules.IsSource(JObject.Parse(json), domain, o ?? new TargetOptions(), new HashSet<string>(pathKeys));
    }

    [Fact]
    public void Sand_IsSource() => Assert.True(IsSource("{code:'sand', blockmaterial:'Sand'}"));

    [Fact]
    public void DirtyGravel_IsSource() => Assert.True(IsSource("{code:'dirtygravel', blockmaterial:'Gravel'}"));

    [Fact]
    public void StonePathFullBlock_IsNotSource()
        => Assert.False(IsSource("{code:'stonepathfullblock', blockmaterial:'Gravel'}", "game", null, "game:stonepath"));

    [Fact]
    public void ModdedPathSlab_IsNotSource()
        => Assert.False(IsSource("{code:'gravelpathslab', blockmaterial:'Gravel'}", "addsomepaths", null, "addsomepaths:gravelpath"));

    [Fact]
    public void ForestFloor_IsSourceOnlyByExtra()
    {
        const string json = "{code:'forestfloor', class:'BlockForestFloor', blockmaterial:'Soil'}";
        Assert.False(IsSource(json));
        Assert.True(IsSource(json, "game", new TargetOptions { ExtraSources = ["game:forestfloor"] }));
    }

    [Fact]
    public void RammedEarth_IsNotSource() => Assert.False(IsSource("{code:'rammed', blockmaterial:'Soil'}"));

    [Fact]
    public void ExcludedSource_IsNotSource()
        => Assert.False(IsSource("{code:'sand', blockmaterial:'Sand'}", "game", new TargetOptions { ExcludeSources = ["game:sand"] }));
}
