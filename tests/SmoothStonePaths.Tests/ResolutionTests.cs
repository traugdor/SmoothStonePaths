using System.Collections.Generic;
using Xunit;

namespace SmoothStonePaths.Tests;

/// <summary>Texture packs ship block textures larger (or smaller) than the vanilla 32x32; fringes must bake at their size.</summary>
public class ResolutionTests
{
    // Masks and piece layout stay authored on the 32 grid; materials are one flat colour each, so resampling cannot matter.
    private sealed class FlatSources : IFringeSources
    {
        public int[] Mask(bool concave)
        {
            var mask = new int[32 * 32];
            for (int i = 0; i < mask.Length; i++) mask[i] = ((i * 7 + i / 32 * 3) % 5 == 0) ? unchecked((int)0xFF000000) : 0;
            return mask;
        }

        public int[] Material(int material, int size)
        {
            var pixels = new int[size * size];
            System.Array.Fill(pixels, unchecked((int)0xFF000000) | material * 0x010203);
            return pixels;
        }

        public int NativeSize(int material) => 64;

        public PieceGeometry Geometry(Piece piece) => piece.Kind == PieceKind.Inner
            ? new PieceGeometry(28, 0, 32, 4, 24, 26)
            : new PieceGeometry(0, 0, 32, 6, 0, 0);
    }

    private static readonly List<Piece> Pieces = new List<Piece>
    {
        new Piece(PieceKind.Edge, FringeMaterials.Grass, 0, 0),
        new Piece(PieceKind.Edge, 5, 1, 0),
        new Piece(PieceKind.EdgeConcave, 6, 2, 0),
        new Piece(PieceKind.Inner, 7, 3, 0),
    };

    private static int[] Base(int size)
    {
        var pixels = new int[size * size];
        for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
                pixels[z * size + x] = unchecked((int)0xFF000000) | ((x * 32 / size) << 8) | (z * 32 / size);
        return pixels;
    }

    [Fact]
    public void Compose_At16_ShowsOneOfTheTexelsItCovers()
    {
        var sources = new FlatSources();
        FringeImage at32 = FringeCompositor.Compose(Pieces, Base(32), 32, sources);
        FringeImage at16 = FringeCompositor.Compose(Pieces, Base(16), 16, sources);

        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                // A 16x16 texel covers a 2x2 block of the authored grid; rotated pieces may sample any corner of it.
                var covered = new[] { (2 * z) * 32 + 2 * x, (2 * z) * 32 + 2 * x + 1, (2 * z + 1) * 32 + 2 * x, (2 * z + 1) * 32 + 2 * x + 1 };
                int baked = at16.Baked[z * 16 + x];
                Assert.True(baked == Base(16)[z * 16 + x] || System.Array.Exists(covered, i => at32.Baked[i] == baked), $"texel ({x},{z})");
            }
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    public void Compose_AtAnySize_IsThe32ResultScaled(int size)
    {
        var sources = new FlatSources();
        FringeImage at32 = FringeCompositor.Compose(Pieces, Base(32), 32, sources);
        FringeImage scaled = FringeCompositor.Compose(Pieces, Base(size), size, sources);

        Assert.Equal(size * size, scaled.Baked.Length);
        Assert.NotNull(scaled.Grass);
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                // Every texel of the scaled image shows what the 32x32 texel it falls in shows.
                int texel32 = (z * 32 / size) * 32 + x * 32 / size;
                Assert.Equal(at32.Baked[texel32], scaled.Baked[z * size + x]);
                Assert.Equal(at32.Grass[texel32], scaled.Grass[z * size + x]);
            }
        }
    }

    [Fact]
    public void ToWorld_RotatesAtAnySize()
    {
        FringeCompositor.ToWorld(1, 5, 2, 64, out int wx, out int wz);
        Assert.Equal((61, 5), (wx, wz));
        FringeCompositor.ToWorld(2, 5, 2, 64, out wx, out wz);
        Assert.Equal((58, 61), (wx, wz));
    }

    [Fact]
    public void MirroredMargin_IsOnePixelAtAnySize()
    {
        Assert.Equal(1, OverlayQuad.MarginTexels(16));
        Assert.Equal(2, OverlayQuad.MarginTexels(32));
        Assert.Equal(4, OverlayQuad.MarginTexels(64));

        var image = new int[64 * 64];
        for (int i = 0; i < image.Length; i++) image[i] = i;
        int[] padded = FringeCompositor.WithMirroredMargin(image, 64, 4);
        Assert.Equal(72 * 72, padded.Length);
        Assert.Equal(image[0], padded[4 * 72 + 3]);      // first texel mirrored one step out
        Assert.Equal(image[3], padded[4 * 72 + 0]);      // and four steps out
        Assert.Equal(image[63], padded[4 * 72 + 68]);    // last texel mirrored one step out on the far side
    }

    [Fact]
    public void Orient_KeepsTheTextureSize()
    {
        var identity = new TopFace.Mapping(0, 0, 1, 0, 0, 1);
        var texture = new int[64 * 64];
        for (int i = 0; i < texture.Length; i++) texture[i] = i;
        Assert.Equal(texture, TopFace.Orient(texture, 64, identity));
    }

    [Fact]
    public void Resample_UsesTheNearestTexel()
    {
        int[] twoByTwo = [1, 2, 3, 4];
        Assert.Equal(new[] { 1, 1, 2, 2, 1, 1, 2, 2, 3, 3, 4, 4, 3, 3, 4, 4 }, FringeAssetSources.Resample(twoByTwo, 2, 2, 4));
        Assert.Equal(new[] { 1 }, FringeAssetSources.Resample(twoByTwo, 2, 2, 1));
        Assert.Same(twoByTwo, FringeAssetSources.Resample(twoByTwo, 2, 2, 2));
    }

    [Theory]
    [InlineData(16, 16, true)]
    [InlineData(32, 32, true)]
    [InlineData(128, 128, true)]
    [InlineData(24, 24, false)]
    [InlineData(64, 32, false)]
    [InlineData(0, 0, false)]
    public void BakeableSize_IsSquareAndAMultipleOf16(int width, int height, bool expected)
        => Assert.Equal(expected, FringeBaker.IsBakeableSize(width, height));
}
