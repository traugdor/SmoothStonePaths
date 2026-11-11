using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace SmoothStonePaths.Tests;

/// <summary>With the default config, targeting finds what the hand-written patches did, plus the blocks added on purpose.</summary>
public class ParityTests
{
    private static readonly TargetOptions Defaults = new SmoothStonePathsConfig { TerrainBlending = true }.ToOptions();

    private static List<(AssetLocation Location, JObject Json)> Vanilla()
    {
        string root = Environment.GetEnvironmentVariable("VINTAGE_STORY");
        Assert.False(string.IsNullOrEmpty(root), "VINTAGE_STORY must point at the game install to compare against its blocktypes.");

        var types = new List<(AssetLocation, JObject)>();
        foreach (string folder in new[] { "survival", "game" })
        {
            string dir = Path.Combine(root, "assets", folder, "blocktypes");
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(Path.Combine(root, "assets", folder), file).Replace(Path.DirectorySeparatorChar, '/');
                types.Add((new AssetLocation("game", relative), JObject.Parse(File.ReadAllText(file))));
            }
        }
        Assert.NotEmpty(types);
        return types;
    }

    private static List<(AssetLocation Location, JObject Json)> Fixtures()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        return Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
            .Select(file => (new AssetLocation(Path.GetFileName(Path.GetDirectoryName(file)), "blocktypes/" + Path.GetFileName(file)), JObject.Parse(File.ReadAllText(file))))
            .ToList();
    }

    private static string Key((AssetLocation Location, JObject Json) type) => type.Location.Domain + ":" + (string)type.Json["code"];

    private static bool IsReceiver(JObject json) => ((string)json["class"] ?? "").StartsWith("BlockBlended", StringComparison.Ordinal);

    private static bool IsSource(JObject json) => json["attributes"]?[BlocktypeRewriter.SourceAttribute]?.Value<bool>() == true;

    [Fact]
    public void VanillaReceivers_AreTodaysSetPlusSlabAndCobblestone()
    {
        BlockTargetingSystem.Rewrite(Vanilla(), Defaults, _ => { }, out var changed);
        string[] receivers = changed.Where(c => IsReceiver(c.Json)).Select(Key).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "game:bonysoil", "game:cobblestone", "game:cobblestonefan", "game:devastatedsoil", "game:drypackeddirt", "game:gravel",
                             "game:packeddirt", "game:rammed", "game:rock", "game:sand", "game:sandwavy", "game:soil",
                             "game:stonepath", "game:stonepathslab" }, receivers);

        // The layered sand, gravel and bony soil share codes with full blocks above but are different blocks.
        Assert.DoesNotContain(changed, c => IsReceiver(c.Json) && c.Location.Path.Contains("layered"));
    }

    [Fact]
    public void VanillaSources_IncludeTheRequestedSoils()
    {
        BlockTargetingSystem.Rewrite(Vanilla(), Defaults, _ => { }, out var changed);
        HashSet<string> sources = changed.Where(c => IsSource(c.Json)).Select(Key).ToHashSet();
        foreach (string expected in new[] { "game:soil", "game:forestfloor", "game:cob", "game:packeddirt", "game:drypackeddirt",
                                            "game:farmland", "game:sand", "game:gravel", "game:sandwavy" })
        {
            Assert.Contains(expected, sources);
        }
        foreach (string never in new[] { "game:stonepath", "game:stonepathfullblock", "game:stonepathslab", "game:stonepathstairs", "game:rammed", "game:mudbrick" })
        {
            Assert.DoesNotContain(never, sources);
        }
    }

    [Fact]
    public void ModFixtures_AreTargeted()
    {
        BlockTargetingSystem.Rewrite(Fixtures(), Defaults, _ => { }, out var changed);
        Dictionary<string, string> classes = changed.Where(c => IsReceiver(c.Json)).ToDictionary(Key, c => (string)c.Json["class"]);

        Assert.Equal(new Dictionary<string, string>
        {
            ["addsomepaths:gravelpath"] = "BlockBlendedPath",
            ["addsomepaths:packedpath"] = "BlockBlendedPath",
            ["moreroads:stonepath"] = "BlockBlendedPath",
            ["moreroads:ancientroad"] = "BlockBlendedPath",
            ["moreroads:clayroad"] = "BlockBlendedPath",
            ["addsomepaths:gravelpathslab"] = "BlockBlendedSlab",
            ["moreroads:stonepathslab"] = "BlockBlendedSlab",
            ["moreroads:ancientroadslab"] = "BlockBlendedSlab",
            ["moreroads:clayroadslab"] = "BlockBlendedSlab",
        }.OrderBy(p => p.Key), classes.OrderBy(p => p.Key));
        Assert.DoesNotContain(changed, c => IsSource(c.Json));
    }
}
