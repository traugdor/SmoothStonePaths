namespace SmoothStonePaths;

/// <summary>
/// A path slab. Its top is half a block below the ground beside it, so loose sand, gravel and dirt look spilled onto
/// it, but grass would not grow there, and the grass overlay is never drawn on it.
/// </summary>
public class BlockBlendedSlab : BlockBlendedPath
{
    protected override bool AcceptsFringe(int material) => material != FringeMaterials.Grass;
}
