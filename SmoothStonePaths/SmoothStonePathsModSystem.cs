using Vintagestory.API.Common;

namespace SmoothStonePaths;

public class SmoothStonePathsModSystem : ModSystem
{
    // Runs on both sides: the client needs the classes to tessellate the fringe, and the server needs them to create
    // the blocks BlockTargetingSystem gives these classes.
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockBlendedPath", typeof(BlockBlendedPath));
        api.RegisterBlockClass("BlockBlendedSlab", typeof(BlockBlendedSlab));
        api.RegisterBlockClass("BlockBlendedTerrain", typeof(BlockBlendedTerrain));
        api.RegisterBlockClass("BlockBlendedSoil", typeof(BlockBlendedSoil));
    }
}
