using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SmoothStonePaths;

/// <summary>
/// Reads the masks, the material textures and the piece shapes from the game's assets, each only once. One instance
/// is shared by every fringed block; <see cref="Generation"/> changes when the textures are reloaded, so blocks know
/// to drop what they built from the old ones.
/// </summary>
internal sealed class FringeAssetSources : IFringeSources
{
    private static FringeAssetSources instance;

    private readonly ICoreClientAPI capi;
    private readonly object sync = new object();
    private readonly Dictionary<bool, int[]> masks = new Dictionary<bool, int[]>();
    private readonly Dictionary<int, (int[] Pixels, int Width, int Height)> materials = new Dictionary<int, (int[], int, int)>();
    private readonly Dictionary<(int, int), int[]> resampled = new Dictionary<(int, int), int[]>();
    private readonly Dictionary<(PieceKind, int), PieceGeometry> geometries = new Dictionary<(PieceKind, int), PieceGeometry>();
    private readonly Dictionary<int, int> sourceIds = new Dictionary<int, int>();
    private readonly FringeSourceRegistry registry;

    /// <summary>Counts texture reloads.</summary>
    public int Generation { get; private set; }

    private FringeAssetSources(ICoreClientAPI capi)
    {
        this.capi = capi;
        registry = new FringeSourceRegistry(message => capi.Logger.Warning(message));
        FringeMaterials.Registry = registry;
        FringeMaterials.ResolveSource = SourceId;
        capi.Event.ReloadTextures += () =>
        {
            lock (sync)
            {
                masks.Clear();
                materials.Clear();
                resampled.Clear();
                geometries.Clear();
                sourceIds.Clear();
                Generation++;
            }
        };
    }

    public static FringeAssetSources For(ICoreClientAPI capi)
    {
        lock (typeof(FringeAssetSources))
        {
            if (instance == null || instance.capi != capi) instance = new FringeAssetSources(capi);
            return instance;
        }
    }

    public int[] Mask(bool concave)
    {
        lock (sync)
        {
            if (!masks.TryGetValue(concave, out int[] mask))
            {
                var location = new AssetLocation("smoothstonepaths", "textures/block/fringe/" + (concave ? "cutout-concave" : "cutout") + ".png");
                (int[] pixels, int width, int height) = Load(location);
                // The masks are this mod's own, authored on the 32x32 grid; a pack that replaces them must keep that size.
                if (width != FringeCompositor.Size || height != FringeCompositor.Size)
                {
                    throw new InvalidOperationException("Mask " + location + " is " + width + "x" + height + ", not 32x32");
                }
                mask = pixels;
                masks[concave] = mask;
            }
            return mask;
        }
    }

    public int[] Material(int material, int size)
    {
        lock (sync)
        {
            if (resampled.TryGetValue((material, size), out int[] pixels)) return pixels;
            (int[] own, int width, int height) = Native(material);
            pixels = Resample(own, width, height, size);
            resampled[(material, size)] = pixels;
            return pixels;
        }
    }

    public int NativeSize(int material)
    {
        lock (sync) return Native(material).Width;
    }

    // The material's texture at its own size, which a texture pack decides. Call with the lock held.
    private (int[] Pixels, int Width, int Height) Native(int material)
    {
        if (materials.TryGetValue(material, out var native)) return native;
        if (material == FringeMaterials.Grass)
        {
            var path = new AssetLocation(FringeMaterials.GrassTexture);
            native = Load(new AssetLocation(path.Domain, "textures/" + path.Path + ".png"));
        }
        else
        {
            native = LoadComposite(registry.TextureName(material));
        }
        materials[material] = native;
        return native;
    }

    /// <summary>Nearest-neighbour resample of a width x height image to size x size.</summary>
    internal static int[] Resample(int[] pixels, int width, int height, int size)
    {
        if (width == size && height == size) return pixels;
        var result = new int[size * size];
        for (int y = 0; y < size; y++)
        {
            int sy = y * height / size;
            for (int x = 0; x < size; x++) result[y * size + x] = pixels[sy * width + x * width / size];
        }
        return result;
    }

    public PieceGeometry Geometry(Piece piece)
    {
        lock (sync)
        {
            var key = (piece.Kind, piece.Variant);
            if (!geometries.TryGetValue(key, out PieceGeometry geometry))
            {
                var location = new AssetLocation("smoothstonepaths", "shapes/block/fringe/" + BlockFringed.ShapeName(piece) + ".json");
                // "Shape" alone would bind to Block.Shape (a CompositeShape) inside a block, so the type is fully qualified here too.
                Vintagestory.API.Common.Shape shape = Vintagestory.API.Common.Shape.TryGet(capi, location);
                PieceGeometry? read = PieceGeometry.FromShape(shape);
                if (read == null) throw new InvalidOperationException("Fringe shape " + location + " is missing or not in the expected form");
                geometry = read.Value;
                geometries[key] = geometry;
            }
            return geometry;
        }
    }

    /// <summary>
    /// The material id of a block the server marked as a source: its own top texture, registered the first time a
    /// block with it is seen. Called from the tessellation threads.
    /// </summary>
    private int SourceId(Block block)
    {
        lock (sync)
        {
            if (sourceIds.TryGetValue(block.Id, out int known)) return known;
        }
        int id = ResolveSourceId(block);
        lock (sync) sourceIds[block.Id] = id;
        return id;
    }

    private int ResolveSourceId(Block block)
    {
        if (block.Textures == null) return FringeMaterials.None;
        string key = FringeSourceRegistry.TopTextureKey(block.Textures.Keys);
        BakedCompositeTexture baked = key == null ? null : block.Textures[key]?.Baked;
        if (baked?.BakedName == null) return FringeMaterials.None;

        TextureAtlasPosition position = capi.BlockTextureAtlas.Positions[baked.TextureSubId];
        int width = (int)Math.Round((position.x2 - position.x1) * capi.BlockTextureAtlas.Size.Width);
        int height = (int)Math.Round((position.y2 - position.y1) * capi.BlockTextureAtlas.Size.Height);
        return registry.Register(baked.BakedName.ToString(), FringeMaterials.CategoryOf(block.BlockMaterial), width, height);
    }

    // Composite names (with overlays or blended overlays) are resolved the same way the baker loads a block's own top.
    private (int[] Pixels, int Width, int Height) LoadComposite(string name)
    {
        IBitmap bitmap = capi.BlockTextureAtlas.LoadCompositeBitmap(new AssetLocationAndSource(name));
        try
        {
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0) throw new InvalidOperationException("Fringe texture " + name + " could not be loaded");
            return (bitmap.Pixels, bitmap.Width, bitmap.Height);
        }
        finally
        {
            (bitmap as IDisposable)?.Dispose();
        }
    }

    private (int[] Pixels, int Width, int Height) Load(AssetLocation location)
    {
        IAsset asset = capi.Assets.TryGet(location);
        if (asset == null) throw new InvalidOperationException("Asset " + location + " not found");
        using (BitmapRef bitmap = asset.ToBitmap(capi))
        {
            return (bitmap.Pixels, bitmap.Width, bitmap.Height);
        }
    }
}
