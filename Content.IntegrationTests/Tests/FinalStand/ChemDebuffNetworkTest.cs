using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.FinalStand;

// These debuffs are read only on the server. Networked without state, every client got them with Until = 0, and the
// shared expiry removed them on the client each tick while each server state put them back.
[TestFixture]
[TestOf(typeof(FSVulnerableComponent))]
public sealed class ChemDebuffNetworkTest : GameTest
{
    [Test]
    public async Task ServerOnlyDebuffsAreNotNetworked()
    {
        var factory = Pair.Server.ResolveDependency<IComponentFactory>();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var type in new[] { typeof(FSVulnerableComponent), typeof(FSArmorSuppressedComponent), typeof(FSArmorPiercingComponent) })
                    Assert.That(factory.GetRegistration(type).NetID, Is.Null, $"{type.Name} is networked");
            });
        });
    }
}
