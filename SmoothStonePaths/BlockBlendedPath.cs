namespace SmoothStonePaths;

/// <summary>
/// A path block that shows the fringe of the grass, sand, gravel or dirt bordering it. Its surface height comes from
/// its own collision box, so it serves stone paths, full-height blocks such as cobblestone, and slabs.
/// </summary>
public class BlockBlendedPath : BlockFringed
{
    protected override float SurfaceLift => Surface.Lift(CollisionBoxes);

    protected override bool ShowsFringe(Vintagestory.API.Common.Block[] chunkExtBlocks, int extIndex3d)
    {
        return !(Variant.TryGetValue("cover", out string cover) && cover == "snow");
    }
}
