using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SmoothStonePaths;

/// <summary>
/// Connected textures (a texture with tiles, as texture packs such as Conquest use) take each face's tile from the
/// block's position, but only when the block is cube-drawn. Drawn from a shape, which the fringe needs, the game picks a
/// whole mesh by a random hash instead. This puts every face back on the tile cube drawing would have used there.
/// </summary>
internal sealed class TileRemapper
{
    private readonly (BakedCompositeTexture[] Tiles, TextureAtlasPosition[] Positions)[] sets;

    /// <summary>The tile choice repeats every this many blocks along each axis.</summary>
    public int Period { get; }

    public TileRemapper((BakedCompositeTexture[] Tiles, TextureAtlasPosition[] Positions)[] sets)
    {
        this.sets = sets;
        int period = 1;
        foreach ((BakedCompositeTexture[] tiles, _) in sets)
        {
            int width = Math.Max(1, tiles[0].TilesWidth);
            int height = Math.Max(1, tiles.Length / width);
            period = Lcm(Lcm(period, width), height);
        }
        Period = period;
    }

    /// <summary>The block's tiled textures with their atlas positions, or null when it has none.</summary>
    public static TileRemapper For(Block block, TextureAtlasPosition[] atlasPositions)
    {
        if (block.Textures == null) return null;
        var sets = new List<(BakedCompositeTexture[], TextureAtlasPosition[])>();
        var seen = new HashSet<BakedCompositeTexture[]>(ReferenceEqualityComparer.Instance);
        foreach (CompositeTexture texture in block.Textures.Values)
        {
            BakedCompositeTexture[] tiles = texture?.Baked?.BakedTiles;
            if (tiles == null || tiles.Length == 0 || !seen.Add(tiles)) continue;
            var positions = new TextureAtlasPosition[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                int subId = tiles[i].TextureSubId;
                positions[i] = subId >= 0 && subId < atlasPositions.Length ? atlasPositions[subId] : null;
            }
            sets.Add((tiles, positions));
        }
        return sets.Count == 0 ? null : new TileRemapper(sets.ToArray());
    }

    /// <summary>Positions with the same key get the same tiles, so meshes can be shared between them.</summary>
    public (int, int, int) PositionKey(int x, int y, int z) => (GameMath.Mod(x, Period), GameMath.Mod(y, Period), GameMath.Mod(z, Period));

    /// <summary>Moves each face that shows one of the tiles onto the tile cube drawing picks at this position.</summary>
    public void Apply(MeshData mesh, int x, int y, int z)
    {
        if (mesh.Uv == null || mesh.Flags == null || mesh.TextureIndices == null || mesh.VerticesPerFace != 4) return;

        var normal = new float[3];
        for (int face = 0; face < mesh.VerticesCount / 4; face++)
        {
            int first = face * 4;
            float u = 0, v = 0;
            for (int i = 0; i < 4; i++)
            {
                u += mesh.Uv[(first + i) * 2];
                v += mesh.Uv[(first + i) * 2 + 1];
            }
            u /= 4; v /= 4;
            int page = face < mesh.TextureIndices.Length ? mesh.TextureIds[mesh.TextureIndices[face]] : -1;

            if (!TryFindTile(page, u, v, out int set, out int current)) continue;

            VertexFlags.UnpackNormal(mesh.Flags[first], normal);
            (BakedCompositeTexture[] tiles, TextureAtlasPosition[] positions) = sets[set];
            int wanted = GameMath.Mod(BakedCompositeTexture.GetTiledTexturesSelector(tiles, FaceSide(normal[0], normal[1], normal[2]), x, y, z), tiles.Length);
            if (wanted == current || positions[wanted] == null) continue;

            TextureAtlasPosition from = positions[current], to = positions[wanted];
            for (int i = 0; i < 4; i++)
            {
                int at = (first + i) * 2;
                mesh.Uv[at] = to.x1 + (mesh.Uv[at] - from.x1) / (from.x2 - from.x1) * (to.x2 - to.x1);
                mesh.Uv[at + 1] = to.y1 + (mesh.Uv[at + 1] - from.y1) / (from.y2 - from.y1) * (to.y2 - to.y1);
            }
            mesh.TextureIndices[face] = FringeBaker.PageSlot(ref mesh.TextureIds, to.atlasTextureId);
        }
    }

    private bool TryFindTile(int page, float u, float v, out int set, out int tile)
    {
        for (set = 0; set < sets.Length; set++)
        {
            TextureAtlasPosition[] positions = sets[set].Positions;
            for (tile = 0; tile < positions.Length; tile++)
            {
                TextureAtlasPosition position = positions[tile];
                if (position != null && position.atlasTextureId == page && position.ContainsUV(u, v)) return true;
            }
        }
        set = tile = -1;
        return false;
    }

    /// <summary>The side a face looks towards, in the game's order: north, east, south, west, up, down.</summary>
    internal static int FaceSide(float x, float y, float z)
    {
        float ax = Math.Abs(x), ay = Math.Abs(y), az = Math.Abs(z);
        if (ay >= ax && ay >= az) return y > 0 ? BlockFacing.indexUP : BlockFacing.indexDOWN;
        if (ax >= az) return x > 0 ? BlockFacing.indexEAST : BlockFacing.indexWEST;
        return z > 0 ? BlockFacing.indexSOUTH : BlockFacing.indexNORTH;
    }

    private static int Lcm(int a, int b) => a / Gcd(a, b) * b;

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}
