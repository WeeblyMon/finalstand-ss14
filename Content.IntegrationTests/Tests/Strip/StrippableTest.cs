using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Strip.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Strip;

public sealed class StrippableTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task DragDropDoesNotOpenStrip()
    {
        await SpawnTarget("MobHuman");

        var userInterface = Comp<UserInterfaceComponent>(Target);
        Assert.That(userInterface.Actors, Is.Empty);

        await DragDrop(Target.Value, Player);

        Assert.That(userInterface.Actors, Is.Empty);

        Assert.That(CUiSys.IsUiOpen(CTarget.Value, StrippingUiKey.Key), Is.False);
        Assert.That(SUiSys.IsUiOpen(STarget.Value, StrippingUiKey.Key), Is.False);
    }
}
