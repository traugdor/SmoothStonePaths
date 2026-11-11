using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace SmoothStonePaths;

internal sealed record TargetSummary(int Paths, int Slabs, int Terrain, int Soil, int Sources, int SkippedCustomClass);

/// <summary>
/// Finds the blocks that receive or give a fringe in every mod's blocktypes and rewrites their JSON before the game
/// creates any block, so other mods' paths and terrain need no patch of their own.
/// </summary>
/// <remarks>
/// Runs after the game's JSON patch loader (0.05), so every mod's patches are already applied, and before the
/// blocktype loader (0.2) creates the blocks. Only the server loads blocktypes; clients receive the resulting classes,
/// drawtypes and attributes with the blocks.
/// </remarks>
public class BlockTargetingSystem : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override double ExecuteOrder() => 0.15;

    public override void AssetsLoaded(ICoreAPI api)
    {
        TargetOptions options = SmoothStonePathsConfig.Load(api, Mod.Logger).ToOptions();

        var assets = new Dictionary<AssetLocation, IAsset>();
        var blocktypes = new List<(AssetLocation Location, JObject Json)>();
        foreach (IAsset asset in api.Assets.GetMany("blocktypes/"))
        {
            if (!asset.Location.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                blocktypes.Add((asset.Location, asset.ToObject<JObject>()));
                assets[asset.Location] = asset;
            }
            catch (Exception e)
            {
                Mod.Logger.Warning("Skipping {0} for fringe targeting, it could not be read: {1}", asset.Location, e.Message);
            }
        }

        TargetSummary summary = Rewrite(blocktypes, options, line => Mod.Logger.VerboseDebug(line), out var changed);

        foreach ((AssetLocation location, JObject json) in changed)
        {
            IAsset asset = assets[location];
            asset.Data = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            asset.IsPatched = true;
            Mod.Logger.VerboseDebug("Fringe targeting changed {0} ({1})", location, (string)TargetRules.Get(json, "class") ?? "source only");
        }

        Mod.Logger.Notification("Fringe targeting: {0} path, {1} slab, {2} terrain, {3} soil and {4} source blocktypes; {5} skipped for a custom class.",
            summary.Paths, summary.Slabs, summary.Terrain, summary.Soil, summary.Sources, summary.SkippedCustomClass);
    }

    /// <summary>
    /// Classifies and rewrites the blocktypes in two passes: the first finds the paths, the second lets slabs and
    /// source exclusions see them. <paramref name="log"/> gets one line per blocktype skipped for being unreadable,
    /// having no code, or having a class of its own while otherwise looking like a receiver.
    /// </summary>
    internal static TargetSummary Rewrite(IReadOnlyList<(AssetLocation Location, JObject Json)> blocktypes, TargetOptions options, Action<string> log,
        out List<(AssetLocation Location, JObject Json)> changed)
    {
        // A blocktype the rules cannot read (a null where a number belongs, an object where a name belongs) is
        // skipped on its own, so one odd file from any mod cannot stop the rest from being targeted.
        var unreadable = new HashSet<AssetLocation>();
        var noPaths = new HashSet<string>();
        var pathKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((AssetLocation location, JObject json) in blocktypes)
        {
            try
            {
                if (TargetRules.ClassifyReceiver(json, location.Domain, options, noPaths).Kind == TargetKind.Path)
                {
                    pathKeys.Add(location.Domain + ":" + (string)TargetRules.Get(json, "code"));
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is InvalidCastException)
            {
                unreadable.Add(location);
                log(location + ": skipped, " + e.Message);
            }
        }

        changed = new List<(AssetLocation, JObject)>();
        int paths = 0, slabs = 0, terrain = 0, soil = 0, sources = 0, custom = 0;
        foreach ((AssetLocation location, JObject json) in blocktypes)
        {
            if (unreadable.Contains(location)) continue;

            TargetDecision decision;
            bool source;
            try
            {
                decision = TargetRules.ClassifyReceiver(json, location.Domain, options, pathKeys);
                source = TargetRules.IsSource(json, location.Domain, options, pathKeys);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is InvalidCastException)
            {
                log(location + ": skipped, " + e.Message);
                continue;
            }

            if (decision.Reason == "no code" || decision.Reason.StartsWith("custom class", StringComparison.Ordinal))
            {
                log(location + ": " + decision.Reason);
                if (decision.Reason != "no code") custom++;
            }

            if (decision.Kind == TargetKind.None && !source) continue;

            BlocktypeRewriter.Apply(json, decision.Kind, source);
            changed.Add((location, json));
            switch (decision.Kind)
            {
                case TargetKind.Path: paths++; break;
                case TargetKind.Slab: slabs++; break;
                case TargetKind.Terrain: terrain++; break;
                case TargetKind.Soil: soil++; break;
            }
            if (source) sources++;
        }
        return new TargetSummary(paths, slabs, terrain, soil, sources, custom);
    }
}
