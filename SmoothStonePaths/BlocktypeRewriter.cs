using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SmoothStonePaths;

/// <summary>Turns a blocktype's JSON into a receiver, a source, or both, in place, before the game creates the block.</summary>
internal static class BlocktypeRewriter
{
    /// <summary>Block attribute that marks a fringe source; clients read it from the block they receive.</summary>
    public const string SourceAttribute = "smoothStonePathsSource";

    public static void Apply(JObject blocktype, TargetKind kind, bool source)
    {
        if (kind != TargetKind.None)
        {
            Set(blocktype, "class", ClassFor(kind));
            DrawFromShape(blocktype);
        }

        if (source)
        {
            if (TargetRules.Get(blocktype, "attributes") is not JObject attributes)
            {
                attributes = new JObject();
                Set(blocktype, "attributes", attributes);
            }
            attributes[SourceAttribute] = true;
        }
    }

    private static string ClassFor(TargetKind kind)
    {
        switch (kind)
        {
            case TargetKind.Slab: return "BlockBlendedSlab";
            case TargetKind.Terrain: return "BlockBlendedTerrain";
            case TargetKind.Soil: return "BlockBlendedSoil";
            default: return "BlockBlendedPath";
        }
    }

    // The fringe hook only runs for blocks drawn from a shape, so cube-drawn receivers are switched to the same cube
    // drawn as a shape. With a drawtypeByType map only its cube entries change, since the map decides per variant.
    private static void DrawFromShape(JObject blocktype)
    {
        if (TargetRules.Get(blocktype, "drawtypeByType") is JObject byType)
        {
            foreach (JProperty entry in byType.Properties())
            {
                if (IsCube(entry.Value)) entry.Value = "Json";
            }
            return;
        }

        JToken drawtype = TargetRules.Get(blocktype, "drawtype");
        if (drawtype == null || IsCube(drawtype)) Set(blocktype, "drawtype", "Json");
    }

    private static bool IsCube(JToken value) => string.Equals((string)value, "Cube", StringComparison.OrdinalIgnoreCase);

    // Replaces the property whatever its case, or adds it.
    private static void Set(JObject json, string name, JToken value)
    {
        JProperty existing = json.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) existing.Value = value;
        else json[name] = value;
    }
}
