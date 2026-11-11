using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SmoothStonePaths.Tests;

/// <summary>
/// Connected textures (a texture with tiles) pick a tile per face from the block's position when cube-drawn. Drawn from
/// a shape the game picks one at random, so the remapper puts each face back on the tile cube drawing would use.
/// </summary>
public class TileRemapperTests
{
    private const int Page = 7;

    private static BakedCompositeTexture[] Tiles(int width, int count)
    {
        var tiles = new BakedCompositeTexture[count];
        for (int i = 0; i < count; i++)
        {
            tiles[i] = new BakedCompositeTexture { BakedName = new AssetLocation("game", "block/test/" + (i + 1)), TextureSubId = i, TilesWidth = width };
        }
        return tiles;
    }

    // Tile i sits in its own 0.1-wide column of the atlas page.
    private static TextureAtlasPosition[] Positions(int count)
    {
        var positions = new TextureAtlasPosition[count];
        for (int i = 0; i < count; i++)
        {
            positions[i] = new TextureAtlasPosition { atlasTextureId = Page, x1 = i * 0.1f, x2 = i * 0.1f + 0.05f, y1 = 0.2f, y2 = 0.25f };
        }
        return positions;
    }

    // One quad facing up, textured with the given tile's full rectangle.
    private static MeshData UpFace(TextureAtlasPosition tile)
    {
        var mesh = new MeshData(4, 6, false, true, true, true);
        mesh.xyz = new float[] { 0, 1, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1 };
        mesh.Uv = new float[] { tile.x1, tile.y1, tile.x2, tile.y1, tile.x2, tile.y2, tile.x1, tile.y2 };
        int up = BlockFacing.UP.NormalPackedFlags;
        mesh.Flags = new[] { up, up, up, up };
        mesh.VerticesCount = 4;
        mesh.TextureIds = new[] { Page };
        mesh.TextureIndices = new byte[] { 0 };
        return mesh;
    }

    private static int TileUnder(MeshData mesh, TextureAtlasPosition[] positions)
    {
        float u = (mesh.Uv[0] + mesh.Uv[4]) / 2, v = (mesh.Uv[1] + mesh.Uv[5]) / 2;
        return System.Array.FindIndex(positions, p => u > p.x1 && u < p.x2 && v > p.y1 && v < p.y2);
    }

    [Theory]
    [InlineData(0f, 0f, -1f, 0)]
    [InlineData(1f, 0f, 0f, 1)]
    [InlineData(0f, 0f, 1f, 2)]
    [InlineData(-1f, 0f, 0f, 3)]
    [InlineData(0f, 1f, 0f, 4)]
    [InlineData(0f, -1f, 0f, 5)]
    public void FaceSide_FollowsTheBlockFacingOrder(float x, float y, float z, int side)
        => Assert.Equal(side, TileRemapper.FaceSide(x, y, z));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(5, 3, -2)]
    [InlineData(-7, 12, 9)]
    public void Apply_PutsTheFaceOnTheTileCubeDrawingWouldUse(int x, int y, int z)
    {
        BakedCompositeTexture[] tiles = Tiles(2, 4);
        TextureAtlasPosition[] positions = Positions(4);
        var remapper = new TileRemapper([(tiles, positions)]);
        MeshData mesh = UpFace(positions[3]);

        remapper.Apply(mesh, x, y, z);

        int expected = GameMath.Mod(BakedCompositeTexture.GetTiledTexturesSelector(tiles, BlockFacing.indexUP, x, y, z), tiles.Length);
        Assert.Equal(expected, TileUnder(mesh, positions));
    }

    [Fact]
    public void Apply_LeavesFacesOfOtherTexturesAlone()
    {
        var remapper = new TileRemapper([(Tiles(2, 4), Positions(4))]);
        var elsewhere = new TextureAtlasPosition { atlasTextureId = Page, x1 = 0.8f, x2 = 0.85f, y1 = 0.8f, y2 = 0.85f };
        MeshData mesh = UpFace(elsewhere);
        float[] before = (float[])mesh.Uv.Clone();

        remapper.Apply(mesh, 1, 2, 3);

        Assert.Equal(before, mesh.Uv);
    }

    [Fact]
    public void Period_RepeatsTheTileChoice()
    {
        var remapper = new TileRemapper([(Tiles(2, 4), Positions(4)), (Tiles(3, 9), Positions(9))]);
        Assert.Equal(6, remapper.Period);
        Assert.Equal(remapper.PositionKey(1, 2, 3), remapper.PositionKey(7, 8, 9));
        Assert.Equal(remapper.PositionKey(-1, 0, 0), remapper.PositionKey(5, 0, 0));
    }
}
