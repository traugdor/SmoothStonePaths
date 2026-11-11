using System;
using System.Linq;
using Vintagestory.API.Common;

namespace SmoothStonePaths;

/// <summary>
/// Contents of ModConfig/smoothstonepaths.json. Changes take effect after a restart, and on a server the server's file
/// applies to every player.
/// </summary>
/// <remarks>
/// Block entries are <c>domain:code</c>, where code is the blocktype's code without its variants, for example
/// <c>game:cobblestone</c> or <c>moreroads:clayroad</c>. <c>*</c> matches anything, and an entry without a domain means
/// <c>game</c>.
/// </remarks>
public class SmoothStonePathsConfig
{
    public const string FileName = "smoothstonepaths.json";

    /// <summary>Also blend grass onto natural sand, gravel, soil and rock, and sand onto gravel. Off by default.</summary>
    public bool TerrainBlending = false;

    /// <summary>Blocks that take every fringe like a path, besides the paths found by their 15/16 height.</summary>
    /// <remarks>Cobble skulls are left out on purpose: their skull pattern covers the top face, and a fringe would trim it.</remarks>
    public string[] IncludePaths = ["game:cobblestone", "game:cobblestonefan"];

    /// <summary>Slabs that take sand, gravel and dirt fringes, besides the slabs of the paths found.</summary>
    public string[] IncludeSlabs = [];

    /// <summary>Blocks that grass grows onto when terrain blending is on, besides full blocks of sand, gravel and soil.</summary>
    public string[] IncludeTerrain = ["game:rock", "game:devastatedsoil"];

    /// <summary>Blocks that never take a fringe.</summary>
    public string[] Exclude =
    [
        "moreroads:stoneroad3", "game:stonepathfullblock", "game:strawbedding", "game:mudbrick", "game:creativegrass",
        "game:dirtygravel", "game:muddygravel", "game:sludgygravel", "game:regolith", "game:cob"
    ];

    /// <summary>Blocks whose top fringes onto their neighbours, besides every block of sand and gravel.</summary>
    public string[] ExtraSources = ["game:soil", "game:forestfloor", "game:cob", "game:packeddirt", "game:drypackeddirt", "game:farmland"];

    /// <summary>Blocks that never fringe onto their neighbours.</summary>
    public string[] ExcludeSources = [];

    internal TargetOptions ToOptions() => new TargetOptions
    {
        TerrainBlending = TerrainBlending,
        IncludePaths = WithDomain(IncludePaths),
        IncludeSlabs = WithDomain(IncludeSlabs),
        IncludeTerrain = WithDomain(IncludeTerrain),
        Exclude = WithDomain(Exclude),
        ExtraSources = WithDomain(ExtraSources),
        ExcludeSources = WithDomain(ExcludeSources)
    };

    private static string[] WithDomain(string[] entries)
        => (entries ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Contains(':') ? e : "game:" + e).ToArray();

    /// <summary>Reads the file, writing the defaults when there is none. A file that cannot be read is kept for the player to fix.</summary>
    public static SmoothStonePathsConfig Load(ICoreAPI api, ILogger logger)
    {
        SmoothStonePathsConfig config;
        try
        {
            config = api.LoadModConfig<SmoothStonePathsConfig>(FileName);
        }
        catch (Exception e)
        {
            logger.Error("Could not read ModConfig/{0}, using defaults: {1}", FileName, e.Message);
            return new SmoothStonePathsConfig();
        }

        if (config == null)
        {
            config = new SmoothStonePathsConfig();
            api.StoreModConfig(config, FileName);
        }
        return config;
    }
}
