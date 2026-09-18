using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class SurgeryGuideTest : GameTest
{
    [Test]
    public async Task EveryGuidedOperationExists()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var guides = protoMan.EnumeratePrototypes<FSSurgeryGuidePrototype>().ToArray();
            Assert.That(guides, Is.Not.Empty, "No surgery guide is defined.");

            foreach (var guide in guides)
            {
                Assert.That(guide.Goals, Is.Not.Empty, $"Guide '{guide.ID}' lists no goals.");

                foreach (var id in guide.Goals.Concat(guide.Access).Concat(guide.Closing))
                {
                    Assert.That(protoMan.HasIndex<EntityPrototype>(id), Is.True,
                        $"Guide '{guide.ID}' names surgery '{id}', which does not exist.");
                }
            }
        });
    }

    [Test]
    public async Task NoOperationHasTwoRoles()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var guide in protoMan.EnumeratePrototypes<FSSurgeryGuidePrototype>())
            {
                var seen = new Dictionary<string, string>();

                foreach (var (role, ids) in new[]
                         {
                             ("goals", guide.Goals),
                             ("access", guide.Access),
                             ("closing", guide.Closing),
                         })
                {
                    foreach (var id in ids)
                    {
                        Assert.That(seen.TryAdd(id.Id, role), Is.True,
                            $"Guide '{guide.ID}' lists '{id}' as both {seen.GetValueOrDefault(id.Id)} and {role}.");
                    }
                }
            }
        });
    }

    [Test]
    public async Task EveryPrerequisiteOfAGoalIsDeclaredAccess()
    {
        var server = Pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            foreach (var guide in protoMan.EnumeratePrototypes<FSSurgeryGuidePrototype>())
            {
                var access = guide.Access.Select(id => id.Id).ToHashSet();

                foreach (var goal in guide.Goals)
                {
                    var current = goal.Id;
                    var depth = 0;

                    while (depth++ < 8)
                    {
                        if (!protoMan.TryIndex<EntityPrototype>(current, out var proto)
                            || !proto.TryGetComponent<SurgeryComponent>(out var surgery, compFactory)
                            || surgery.Requirement is not { } requirement)
                        {
                            break;
                        }

                        Assert.That(access, Does.Contain(requirement.Id),
                            $"Goal '{goal.Id}' requires '{requirement.Id}', which guide '{guide.ID}' does not " +
                            "list under access - it would be recommended as a treatment in its own right.");

                        current = requirement.Id;
                    }
                }
            }
        });
    }
}
