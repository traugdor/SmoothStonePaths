using Vintagestory.API.MathTools;

namespace SmoothStonePaths;

/// <summary>Where a block's top sits relative to a stone path's top, which the fringe pieces are authored for.</summary>
internal static class Surface
{
    /// <summary>Height of a stone path's top, in blocks.</summary>
    public const float PathTop = 15f / 16f;

    /// <summary>How far the top of the highest collision box sits above a path's top: 0 for a path, 1/16 for a full block, -1/2 for a slab.</summary>
    public static float Lift(Cuboidf[] collisionBoxes)
    {
        if (collisionBoxes == null || collisionBoxes.Length == 0) return 0f;
        float top = float.MinValue;
        foreach (Cuboidf box in collisionBoxes) top = System.Math.Max(top, box.Y2);
        return top - PathTop;
    }
}
