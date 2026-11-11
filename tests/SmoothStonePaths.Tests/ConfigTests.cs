using Xunit;

namespace SmoothStonePaths.Tests;

public class ConfigTests
{
    [Fact]
    public void EntryWithoutDomain_MeansGame()
        => Assert.Equal("game:stoneroad3", new SmoothStonePathsConfig { Exclude = ["stoneroad3"] }.ToOptions().Exclude[0]);

    [Fact]
    public void EntryWithDomain_IsKept()
        => Assert.Equal("moreroads:stoneroad3", new SmoothStonePathsConfig { Exclude = ["moreroads:stoneroad3"] }.ToOptions().Exclude[0]);

    [Fact]
    public void Defaults_KeepTerrainBlendingOff() => Assert.False(new SmoothStonePathsConfig().TerrainBlending);

    [Fact]
    public void Defaults_IncludeCobblestoneAndTheNewSources()
    {
        var config = new SmoothStonePathsConfig();
        Assert.Contains("game:cobblestone", config.IncludePaths);
        Assert.Equal(new[] { "game:soil", "game:forestfloor", "game:cob", "game:packeddirt", "game:drypackeddirt", "game:farmland" }, config.ExtraSources);
    }
}
