using Content.Shared._FinalStand.Humanoid;
using NUnit.Framework;

namespace Content.Tests.Shared._FinalStand;

// Body sizes come from the client's profile. Anything that isn't a real size falls back to the species default
// instead of reaching physics and sprites.
[TestFixture]
public sealed class FSBodySizeTest
{
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    [TestCase(0f)]
    [TestCase(-1f)]
    public void ABadSizeFallsBackToTheDefault(float size)
    {
        var body = new FSBodySize();
        Assert.That(body.ClampHeight(size), Is.EqualTo(body.DefaultHeight));
        Assert.That(body.ClampWidth(size), Is.EqualTo(body.DefaultWidth));
    }

    [Test]
    public void ARealSizeIsKeptWithinTheLimits()
    {
        var body = new FSBodySize();
        Assert.That(body.ClampHeight(1.1f), Is.EqualTo(1.1f));
        Assert.That(body.ClampHeight(5f), Is.EqualTo(body.MaxHeight));
        Assert.That(body.ClampWidth(0.5f), Is.EqualTo(body.MinWidth));
    }
}
