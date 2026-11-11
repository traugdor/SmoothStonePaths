using Vintagestory.API.Common;

namespace SmoothStonePaths;

/// <summary>
/// Maps neighbouring blocks to fringe material ids. Id 0 means no fringe and 1 is grass; every other id is a source
/// texture handed out by <see cref="FringeSourceRegistry"/>. Only blocks the server marked as sources give a fringe.
/// </summary>
internal static class FringeMaterials
{
    public const int None = 0;
    public const int Grass = 1;

    /// <summary>Bits needed to store one material id in a neighbour key.</summary>
    public const int Bits = 8;

    /// <summary>The texture the grass fringe is cut from, tinted live like the grass around it.</summary>
    public const string GrassTexture = "game:block/plant/grasscoverage/normal-top";

    /// <summary>The client's registry and the function that resolves a source block to its id; set by <see cref="FringeAssetSources"/>.</summary>
    internal static FringeSourceRegistry Registry;
    internal static System.Func<Block, int> ResolveSource;

    /// <summary>
    /// Whether a terrain fringe flows from a neighbour with this material id onto a natural terrain block. Each
    /// boundary flows one way only, so two blocks never fringe onto each other: grass onto sand, gravel and soil,
    /// sand onto gravel.
    /// </summary>
    public static bool FlowsOnto(int material, EnumBlockMaterial receiver) => FlowsOnto(material, receiver, Registry);

    internal static bool FlowsOnto(int material, EnumBlockMaterial receiver, FringeSourceRegistry registry)
    {
        if (material == Grass) return receiver == EnumBlockMaterial.Sand || receiver == EnumBlockMaterial.Gravel || receiver == EnumBlockMaterial.Soil;
        if (material == None || registry == null) return false;
        return receiver == EnumBlockMaterial.Gravel && registry.Category(material) == SourceCategory.Sand;
    }

    public static int Classify(Block neighbour)
    {
        if (neighbour == null || neighbour.Id == 0) return None;
        if (neighbour.Attributes?[BlocktypeRewriter.SourceAttribute].AsBool(false) != true) return None;
        if (neighbour.Variant != null && neighbour.Variant.TryGetValue("grasscoverage", out string coverage) && coverage != "none") return Grass;
        return ResolveSource?.Invoke(neighbour) ?? None;
    }

    /// <summary>Which flow rules a source block's own texture follows, from its block material.</summary>
    public static SourceCategory CategoryOf(EnumBlockMaterial material)
    {
        switch (material)
        {
            case EnumBlockMaterial.Sand: return SourceCategory.Sand;
            case EnumBlockMaterial.Gravel: return SourceCategory.Gravel;
            default: return SourceCategory.Soil;
        }
    }
}
