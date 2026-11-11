using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Util;

namespace SmoothStonePaths;

/// <summary>What a blocktype is turned into, if anything.</summary>
internal enum TargetKind { None, Path, Slab, Terrain, Soil }

internal sealed record TargetDecision(TargetKind Kind, string Reason);

/// <summary>
/// The choices that decide targeting. Entries are <c>domain:code</c>, matched against a blocktype's domain and base
/// code, and may use <c>*</c> wildcards.
/// </summary>
internal sealed class TargetOptions
{
    public bool TerrainBlending;
    public string[] IncludePaths = [];
    public string[] IncludeSlabs = [];
    public string[] IncludeTerrain = [];
    public string[] Exclude = [];
    public string[] ExtraSources = [];
    public string[] ExcludeSources = [];
}

/// <summary>
/// Decides from a blocktype's JSON, before the game creates any block, whether it receives a fringe or gives one.
/// Only blocktypes with no class or the plain Block class are given one of our classes, since replacing another class
/// would remove that block's own behaviour.
/// </summary>
internal static class TargetRules
{
    private const double PathTop = 0.9375;

    private static readonly string[] OurClasses = { "BlockBlendedPath", "BlockBlendedSlab", "BlockBlendedTerrain", "BlockBlendedSoil" };
    private static readonly TargetKind[] OurKinds = { TargetKind.Path, TargetKind.Slab, TargetKind.Terrain, TargetKind.Soil };

    /// <param name="pathKeys">The <c>domain:code</c> keys of the blocktypes found to be paths; empty on the first pass.</param>
    public static TargetDecision ClassifyReceiver(JObject blocktype, string domain, TargetOptions options, IReadOnlySet<string> pathKeys)
    {
        string code = (string)Get(blocktype, "code");
        if (string.IsNullOrEmpty(code)) return new TargetDecision(TargetKind.None, "no code");
        string key = domain + ":" + code;

        if (Matches(options.Exclude, key)) return new TargetDecision(TargetKind.None, "excluded");
        if (Get(blocktype, "classByType") != null) return new TargetDecision(TargetKind.None, "classByType");

        string cls = (string)Get(blocktype, "class");
        int ours = cls == null ? -1 : Array.FindIndex(OurClasses, c => Is(c, cls));
        if (ours >= 0) return new TargetDecision(OurKinds[ours], "already " + cls);

        bool plain = cls == null || Is(cls, "Block");
        bool soilClass = Is(cls, "BlockSoil");
        if (!plain && !soilClass)
        {
            // Only worth reporting when the block looks like something we would have changed.
            bool wouldMatch = HasPathBox(blocktype) || Matches(options.IncludePaths, key) || Matches(options.IncludeSlabs, key)
                || Matches(options.IncludeTerrain, key) || IsSlabOfPath(code, key, pathKeys);
            return new TargetDecision(TargetKind.None, wouldMatch ? "custom class " + cls : "not a receiver");
        }
        if (Contains(code, "stairs")) return new TargetDecision(TargetKind.None, "stairs");

        if (plain)
        {
            if (Matches(options.IncludeSlabs, key)) return new TargetDecision(TargetKind.Slab, "listed slab");
            if (IsSlabOfPath(code, key, pathKeys)) return new TargetDecision(TargetKind.Slab, "slab of a path");
            if (!Contains(code, "slab"))
            {
                if (Matches(options.IncludePaths, key)) return new TargetDecision(TargetKind.Path, "listed path");
                if (HasPathBox(blocktype)) return new TargetDecision(TargetKind.Path, "15/16 collision box");
            }
        }

        if (!options.TerrainBlending) return new TargetDecision(TargetKind.None, "not a path");

        if (soilClass)
        {
            return HasGrassCoverage(blocktype)
                ? new TargetDecision(TargetKind.Soil, "soil with grass coverage")
                : new TargetDecision(TargetKind.None, "soil class without grass coverage");
        }

        if (Matches(options.IncludeTerrain, key)) return new TargetDecision(TargetKind.Terrain, "listed terrain");
        if (IsLooseMaterial(blocktype) && IsFullCube(blocktype) && IsCubeOrJsonDrawn(blocktype))
        {
            return new TargetDecision(TargetKind.Terrain, "full cube of sand, gravel or soil");
        }
        return new TargetDecision(TargetKind.None, "not a receiver");
    }

    /// <summary>
    /// Whether neighbours show this blocktype's fringe. Loose sand and gravel always do, other blocks only when listed.
    /// Paths and everything named after one (their slabs, stairs and full blocks) never do, so paths never fringe each other.
    /// </summary>
    public static bool IsSource(JObject blocktype, string domain, TargetOptions options, IReadOnlySet<string> pathKeys)
    {
        string code = (string)Get(blocktype, "code");
        if (string.IsNullOrEmpty(code)) return false;
        string key = domain + ":" + code;

        if (Matches(options.ExcludeSources, key)) return false;
        if (pathKeys.Any(path => key.StartsWith(path, StringComparison.OrdinalIgnoreCase))) return false;
        if (Matches(options.ExtraSources, key)) return true;
        return ValuesOf(blocktype, "blockmaterial").Any(m => Is((string)m, "Sand") || Is((string)m, "Gravel"));
    }

    internal static bool Matches(string[] entries, string key)
    {
        foreach (string entry in entries)
        {
            if (entry.Contains('*') ? WildcardUtil.Match(entry, key) : Is(entry, key)) return true;
        }
        return false;
    }

    // JSON property names in blocktypes are not case sensitive to the game, so neither are they here.
    internal static JToken Get(JObject json, string name) => json.GetValue(name, StringComparison.OrdinalIgnoreCase);

    // The value itself, or every value of its ByType map; empty when neither is present.
    internal static IEnumerable<JToken> ValuesOf(JObject json, string name)
    {
        JToken single = Get(json, name);
        if (single != null) yield return single;
        if (Get(json, name + "ByType") is JObject byType)
        {
            foreach (KeyValuePair<string, JToken> entry in byType) yield return entry.Value;
        }
    }

    private static bool IsSlabOfPath(string code, string key, IReadOnlySet<string> pathKeys)
        => code.EndsWith("slab", StringComparison.OrdinalIgnoreCase) && pathKeys.Contains(key.Substring(0, key.Length - "slab".Length));

    private static bool HasPathBox(JObject blocktype)
    {
        List<JToken> boxes = ValuesOf(blocktype, "collisionbox").ToList();
        return boxes.Count > 0 && boxes.All(box => box is JObject b
            && Coord(b, "x1", 0) == 0 && Coord(b, "y1", 0) == 0 && Coord(b, "z1", 0) == 0
            && Coord(b, "x2", 1) == 1 && Coord(b, "y2", 1) == PathTop && Coord(b, "z2", 1) == 1);
    }

    private static double Coord(JObject box, string name, double missing)
    {
        JToken value = Get(box, name);
        return value == null ? missing : (double)value;
    }

    private static bool HasGrassCoverage(JObject blocktype)
    {
        if (Get(blocktype, "variantgroups") is not JArray groups) return false;
        return groups.OfType<JObject>().Any(g => Contains((string)Get(g, "code"), "grasscoverage") || Contains((string)Get(g, "loadFromProperties"), "grasscoverage"));
    }

    private static bool IsLooseMaterial(JObject blocktype)
    {
        string material = (string)Get(blocktype, "blockmaterial");
        return Is(material, "Sand") || Is(material, "Gravel") || Is(material, "Soil");
    }

    private static bool IsFullCube(JObject blocktype)
    {
        List<JToken> shapes = ValuesOf(blocktype, "shape").ToList();
        return shapes.All(shape => shape is JObject s && IsCubeShape((string)Get(s, "base")));
    }

    private static bool IsCubeShape(string path) => Is(path, "block/basic/cube") || Is(path, "game:block/basic/cube");

    private static bool IsCubeOrJsonDrawn(JObject blocktype)
        => ValuesOf(blocktype, "drawtype").All(d => Is((string)d, "Cube") || Is((string)d, "Json"));

    private static bool Is(string a, string b) => a != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string text, string part) => text != null && text.Contains(part, StringComparison.OrdinalIgnoreCase);
}
