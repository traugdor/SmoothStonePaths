using System;
using System.Collections.Generic;

namespace SmoothStonePaths;

/// <summary>Which terrain flow rules a source's fringe follows.</summary>
internal enum SourceCategory { Grass, Sand, Gravel, Soil }

/// <summary>
/// Gives every distinct source texture a material id, the first time a neighbour with that texture is seen. Ids fit in
/// the 8 bits each neighbour has in a fringe key; 0 is no fringe and 1 is grass, so textures get 2 to 255.
/// </summary>
internal sealed class FringeSourceRegistry
{
    public const int Capacity = 255;

    private const int FirstId = 2;

    private readonly Action<string> logWarning;
    private readonly object sync = new object();
    private readonly Dictionary<string, int> ids = new Dictionary<string, int>();
    private readonly List<(string Name, SourceCategory Category)> entries = new List<(string, SourceCategory)>();
    private readonly HashSet<string> rejected = new HashSet<string>();
    private bool warnedFull;

    public FringeSourceRegistry(Action<string> logWarning)
    {
        this.logWarning = logWarning;
    }

    /// <summary>The id for this texture, or <see cref="FringeMaterials.None"/> when it cannot be used as a fringe.</summary>
    public int Register(string textureName, SourceCategory category, int width, int height)
    {
        lock (sync)
        {
            if (ids.TryGetValue(textureName, out int id)) return id;
            if (rejected.Contains(textureName)) return FringeMaterials.None;

            // Any size works, since sources are resampled to the size of the block they fringe onto.
            if (width <= 0 || height <= 0)
            {
                rejected.Add(textureName);
                logWarning("[smoothstonepaths] " + textureName + " has no texels, so it gives no fringe.");
                return FringeMaterials.None;
            }
            if (FirstId + entries.Count > Capacity)
            {
                if (!warnedFull) logWarning("[smoothstonepaths] More than " + (Capacity - 1) + " fringe textures; further ones give no fringe.");
                warnedFull = true;
                return FringeMaterials.None;
            }

            id = FirstId + entries.Count;
            entries.Add((textureName, category));
            ids[textureName] = id;
            return id;
        }
    }

    public string TextureName(int id)
    {
        lock (sync) return entries[id - FirstId].Name;
    }

    public SourceCategory Category(int id)
    {
        if (id == FringeMaterials.Grass) return SourceCategory.Grass;
        lock (sync) return entries[id - FirstId].Category;
    }

    /// <summary>
    /// The texture code that covers a block's top: up, else top, else all, else the block's only texture (layered
    /// gravel names its one texture "material"); null if none of these applies.
    /// </summary>
    internal static string TopTextureKey(IEnumerable<string> textureCodes)
    {
        List<string> codes = new List<string>(textureCodes);
        foreach (string code in new[] { "up", "top", "all" })
        {
            foreach (string present in codes)
            {
                if (string.Equals(present, code, StringComparison.OrdinalIgnoreCase)) return present;
            }
        }
        return codes.Count == 1 ? codes[0] : null;
    }
}
