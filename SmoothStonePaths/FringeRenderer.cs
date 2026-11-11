using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SmoothStonePaths;

/// <summary>
/// Draws the fringe for one block type. Blocks that cannot share the <see cref="BlockFringed"/> base class, such as
/// soil, which must stay a BlockSoil, use it by composition.
/// </summary>
internal sealed class FringeRenderer
{
    // Concave strips share the plain strips' shapes; only their texture differs.
    internal static string ShapeName(Piece piece)
    {
        switch (piece.Kind)
        {
            case PieceKind.BleedEast: return "bleed-east";
            case PieceKind.BleedWest: return "bleed-west";
            case PieceKind.Inner: return "inner";
            default:
                switch (piece.Variant)
                {
                    case StripVariant.Long: return "edge-long";
                    case StripVariant.PatternB: return "edge-b";
                    default: return "edge-a";
                }
        }
    }

    private readonly Block block;
    private readonly float surfaceLift;
    private readonly System.Func<int, bool> acceptsFilter;
    private readonly System.Func<Block[], int, bool> showsFringe;
    private ICoreClientAPI capi;
    private FringeAssetSources sources;
    private FringeBaker baker;
    private int generation;
    private readonly object cacheLock = new object();
    private readonly Dictionary<(MeshData baseMesh, long key, int variation, (int, int, int) tilePosition), MeshData> composites =
        new Dictionary<(MeshData, long, int, (int, int, int)), MeshData>();
    private TileRemapper tiles;
    private bool tilesResolved;

    public FringeRenderer(Block block, float surfaceLift, System.Func<int, bool> accepts, System.Func<Block[], int, bool> shows)
    {
        this.block = block;
        this.surfaceLift = surfaceLift;
        acceptsFilter = accepts;
        showsFringe = shows;
    }

    public void Load(ICoreAPI api)
    {
        capi = api as ICoreClientAPI;
        if (capi != null) sources = FringeAssetSources.For(capi);

        // The block's own shape has no color map, so without this the tessellator would not sample the
        // local climate and the grass fringe could not be tinted to match the surrounding grass.
        block.LoadColorMapAnyway = true;
    }

    public void Tesselate(ref MeshData sourceMesh, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
    {
        if (capi == null) return;

        long key = showsFringe(chunkExtBlocks, extIndex3d) ? ComputeKey(chunkExtBlocks, extIndex3d, acceptsFilter) : 0;
        int variation = key == 0 || pos == null ? 0 : FringePlan.ChooseVariation(key, pos.X, pos.Y, pos.Z);

        lock (cacheLock)
        {
            // Everything built from the old textures is stale once the textures have been reloaded.
            if (generation != sources.Generation)
            {
                generation = sources.Generation;
                composites.Clear();
                baker = null;
                tilesResolved = false;
            }
            if (!tilesResolved)
            {
                tiles = TileRemapper.For(block, capi.BlockTextureAtlas.Positions);
                tilesResolved = true;
            }

            // A block with connected textures needs its tiles put back even where it shows no fringe.
            bool tiled = tiles != null && pos != null;
            if (key == 0 && !tiled) return;

            var tilePosition = tiled ? tiles.PositionKey(pos.X, pos.Y, pos.Z) : default;
            var cacheKey = (sourceMesh, key, variation, tilePosition);
            if (!composites.TryGetValue(cacheKey, out MeshData composite))
            {
                MeshData baseMesh = sourceMesh;
                if (tiled)
                {
                    baseMesh = sourceMesh.Clone();
                    tiles.Apply(baseMesh, pos.X, pos.Y, pos.Z);
                }
                composite = key == 0 ? baseMesh : Compose(baseMesh, key, variation);
                composites[cacheKey] = composite;
            }
            sourceMesh = composite;
        }
    }

    // See FringePlan for the key layout.
    internal static long ComputeKey(Block[] chunkExtBlocks, int extIndex3d) => ComputeKey(chunkExtBlocks, extIndex3d, null);

    internal static long ComputeKey(Block[] chunkExtBlocks, int extIndex3d, System.Func<int, bool> accepts)
    {
        int[] move = TileSideEnum.MoveIndex;
        long key = 0;
        for (int side = 0; side < 4; side++)
        {
            key |= (long)ClassifyExposed(chunkExtBlocks, extIndex3d + move[side], accepts) << (side * FringeMaterials.Bits);
        }
        for (int corner = 0; corner < 4; corner++)
        {
            int offset = move[corner] + move[(corner + 1) % 4];
            key |= (long)ClassifyExposed(chunkExtBlocks, extIndex3d + offset, accepts) << ((4 + corner) * FringeMaterials.Bits);
        }
        return key;
    }

    // A neighbour whose top is covered by a block with a solid bottom (stairs, a full block, a bottom slab) shows no
    // grass, sand or dirt, so it gets no fringe. Plants and air above it leave its top exposed.
    internal static int ClassifyExposed(Block[] chunkExtBlocks, int index, System.Func<int, bool> accepts)
    {
        int material = FringeMaterials.Classify(At(chunkExtBlocks, index));
        if (material == FringeMaterials.None) return material;
        if (accepts != null && !accepts(material)) return FringeMaterials.None;
        if (IsCovered(chunkExtBlocks, index)) return FringeMaterials.None;
        return material;
    }

    /// <summary>Whether the block at this index has a block with a solid bottom on top of it.</summary>
    internal static bool IsCovered(Block[] chunkExtBlocks, int index)
    {
        Block above = At(chunkExtBlocks, index + TileSideEnum.MoveIndex[BlockFacing.indexUP]);
        return above != null && above.Id != 0 && above.SideSolid[BlockFacing.indexDOWN];
    }

    private static readonly HashSet<string> reported = new HashSet<string>();

    private MeshData Compose(MeshData baseMesh, long key, int variation)
    {
        List<Piece> planned = FringePlan.Build(key, variation);
        // A block that cannot be baked keeps its own mesh and shows no fringe; TryBake has said why, once.
        return TryBake(baseMesh, planned) ?? baseMesh;
    }

    // The fringe is baked into the block's own top texture, so nothing floats above the surface.
    private MeshData TryBake(MeshData baseMesh, List<Piece> planned)
    {
        if (planned.Count == 0) return null;

        string failure;
        try
        {
            baker ??= new FringeBaker(capi, block, sources);
            MeshData baked = baker.TryBake(baseMesh, planned, surfaceLift, out failure);
            if (baked != null)
            {
                Report("baked", "Fringes are baked into the top textures of " + block.Code.FirstCodePart() + " blocks.", false);
                return baked;
            }
        }
        catch (System.Exception e)
        {
            failure = e.GetType().Name + ": " + e.Message;
        }

        Report("unbaked-" + block.Code.FirstCodePart() + "-" + failure, "No fringe on " + block.Code + ": " + failure + ".", true);
        return null;
    }

    private void Report(string once, string message, bool warning)
    {
        lock (reported)
        {
            if (!reported.Add(once)) return;
        }
        if (warning) capi.Logger.Warning("[smoothstonepaths] " + message);
        else capi.Logger.Notification("[smoothstonepaths] " + message);
    }

    // The inside-block renderer calls this hook with a sparsely filled array, so nulls and bad indices are possible.
    internal static Block At(Block[] blocks, int index)
    {
        return (uint)index < (uint)blocks.Length ? blocks[index] : null;
    }
}
