using System.Collections.Generic;
using Vintagestory.API.Common;
using Xunit;

namespace SmoothStonePaths.Tests;

public class FringeSourceRegistryTests
{
    private readonly List<string> warnings = new List<string>();

    private FringeSourceRegistry NewRegistry() => new FringeSourceRegistry(warnings.Add);

    [Fact]
    public void Register_GivesSameIdForSameTexture()
    {
        FringeSourceRegistry registry = NewRegistry();
        int id = registry.Register("game:block/stone/sand/granite1", SourceCategory.Sand, 32, 32);
        Assert.True(id >= 2);
        Assert.Equal(id, registry.Register("game:block/stone/sand/granite1", SourceCategory.Sand, 32, 32));
        Assert.Equal("game:block/stone/sand/granite1", registry.TextureName(id));
    }

    [Fact]
    public void Register_GivesNewIdForNewTexture()
    {
        FringeSourceRegistry registry = NewRegistry();
        Assert.NotEqual(registry.Register("a", SourceCategory.Sand, 32, 32), registry.Register("b", SourceCategory.Soil, 32, 32));
    }

    [Fact]
    public void Register_AcceptsTexturePackSizes()
    {
        FringeSourceRegistry registry = NewRegistry();
        Assert.NotEqual(FringeMaterials.None, registry.Register("pack:block/sand-hd", SourceCategory.Sand, 128, 128));
        Assert.NotEqual(FringeMaterials.None, registry.Register("pack:block/sand-low", SourceCategory.Sand, 16, 16));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Register_RejectsEmptyTexture()
    {
        FringeSourceRegistry registry = NewRegistry();
        Assert.Equal(FringeMaterials.None, registry.Register("modded:block/broken", SourceCategory.Soil, 0, 0));
        Assert.Single(warnings);
        Assert.Equal(FringeMaterials.None, registry.Register("modded:block/broken", SourceCategory.Soil, 0, 0));
        Assert.Single(warnings);
    }

    [Fact]
    public void Register_StopsAtCapacity()
    {
        FringeSourceRegistry registry = NewRegistry();
        for (int i = 2; i <= FringeSourceRegistry.Capacity; i++)
        {
            Assert.Equal(i, registry.Register("texture" + i, SourceCategory.Soil, 32, 32));
        }
        Assert.Equal(FringeMaterials.None, registry.Register("one too many", SourceCategory.Soil, 32, 32));
        Assert.Equal(FringeMaterials.None, registry.Register("two too many", SourceCategory.Soil, 32, 32));
        Assert.Single(warnings);
    }

    [Fact]
    public void TopTextureKey_PrefersUpThenTopThenAll()
    {
        Assert.Equal("up", FringeSourceRegistry.TopTextureKey(["all", "up"]));
        Assert.Equal("top", FringeSourceRegistry.TopTextureKey(["all", "top"]));
        Assert.Equal("all", FringeSourceRegistry.TopTextureKey(["all"]));
        Assert.Null(FringeSourceRegistry.TopTextureKey(["north", "south"]));
    }

    [Fact]
    public void TopTextureKey_FallsBackToTheOnlyTexture()
        => Assert.Equal("material", FringeSourceRegistry.TopTextureKey(["material"]));

    [Fact]
    public void FlowsOnto_SandAndGravel()
    {
        FringeSourceRegistry registry = NewRegistry();
        int sand = registry.Register("sand", SourceCategory.Sand, 32, 32);
        int soil = registry.Register("soil", SourceCategory.Soil, 32, 32);

        Assert.True(FringeMaterials.FlowsOnto(FringeMaterials.Grass, EnumBlockMaterial.Sand, registry));
        Assert.False(FringeMaterials.FlowsOnto(sand, EnumBlockMaterial.Sand, registry));
        Assert.True(FringeMaterials.FlowsOnto(FringeMaterials.Grass, EnumBlockMaterial.Gravel, registry));
        Assert.True(FringeMaterials.FlowsOnto(sand, EnumBlockMaterial.Gravel, registry));
        Assert.False(FringeMaterials.FlowsOnto(soil, EnumBlockMaterial.Gravel, registry));
        Assert.True(FringeMaterials.FlowsOnto(FringeMaterials.Grass, EnumBlockMaterial.Soil, registry));
        Assert.False(FringeMaterials.FlowsOnto(sand, EnumBlockMaterial.Soil, registry));
    }

    [Fact]
    public void Key_FitsEightNeighboursOfEightBits()
    {
        long key = 0;
        for (int slot = 0; slot < 8; slot++) key |= (long)(slot == 3 ? 255 : 200 + slot) << (slot * FringeMaterials.Bits);
        for (int side = 0; side < 4; side++) Assert.Equal(side == 3 ? 255 : 200 + side, FringePlan.Side(key, side));
        for (int corner = 0; corner < 4; corner++) Assert.Equal(204 + corner, FringePlan.Corner(key, corner));
    }
}
