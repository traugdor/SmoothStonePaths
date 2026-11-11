using Vintagestory.API.MathTools;
using Xunit;

namespace SmoothStonePaths.Tests;

public class SurfaceTests
{
    private static Cuboidf[] Box(float y2) => [new Cuboidf(0, 0, 0, 1, y2, 1)];

    [Fact]
    public void StonePath_HasNoLift() => Assert.Equal(0f, Surface.Lift(Box(0.9375f)), 5);

    [Fact]
    public void FullBlock_IsLiftedOnePixel() => Assert.Equal(1f / 16f, Surface.Lift(Box(1f)), 5);

    [Fact]
    public void Slab_IsLoweredHalfABlock() => Assert.Equal(-0.5f, Surface.Lift(Box(0.4375f)), 5);

    [Fact]
    public void NoBoxes_HasNoLift()
    {
        Assert.Equal(0f, Surface.Lift(null));
        Assert.Equal(0f, Surface.Lift([]));
    }
}
