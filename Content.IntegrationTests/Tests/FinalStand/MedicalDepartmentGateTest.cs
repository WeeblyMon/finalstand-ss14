using Content.IntegrationTests.Fixtures;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedicalDepartmentGateTest : GameTest
{
    private const string MedicalDepartment = "Medical";

    private static readonly string[] ShouldBeMedical =
    {
        "CombatMedic",
        "MedicalDoctor",
        "ChiefMedicalOfficer",
        "Chemist",
        "MedicalIntern",
        "Paramedic",
    };

    private static readonly string[] ShouldNotBeMedical =
    {
        "Captain",
        "StationEngineer",
        "SecurityOfficer",
        "Scientist",
    };

    [Test]
    public async Task MedicalJobsResolveToTheMedicalDepartment()
    {
        var server = Pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var job in ShouldBeMedical)
                {
                    Assert.That(TryGetPrimaryDepartment(protos, job, out var department), Is.True,
                        $"{job} has no primary department at all");
                    Assert.That(department, Is.EqualTo(MedicalDepartment),
                        $"{job} should be Medical, but its primary department is {department}");
                }

                foreach (var job in ShouldNotBeMedical)
                {
                    TryGetPrimaryDepartment(protos, job, out var department);
                    Assert.That(department, Is.Not.EqualTo(MedicalDepartment),
                        $"{job} must not count as medical - it would get the medic-only readout");
                }
            });
        });
    }

    private static bool TryGetPrimaryDepartment(IPrototypeManager protos, string job, out string? department)
    {
        foreach (var proto in protos.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (!proto.Primary || !proto.Roles.Contains(job))
                continue;

            department = proto.ID;
            return true;
        }

        department = null;
        return false;
    }
}
