using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server._FinalStand.MedicalOps;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gravity;
using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using ServerSurgerySystem = Content.Server._Shitmed.Medical.Surgery.SurgerySystem;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class SurgeryFlowTest : GameTest
{
    public sealed class SurgeryCompletedListener : TestListenerSystem<FSSurgeryCompletedEvent>;

    private const string HumanProto = "MobHuman";

    private const string Blunt = "Blunt";

    private static readonly string[] FloorTools = ["Scalpel", "Retractor", "Hemostat", "Cautery"];

    private sealed class Theatre
    {
        public EntityUid Patient;
        public EntityUid Surgeon;
        public EntityUid SurgeonMind;
    }

    private async Task<Theatre> Prepare()
    {
        var map = await Pair.CreateTestMap();
        var theatre = new Theatre();

        await Server.WaitPost(() =>
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            SEntMan.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false, new GasMixture(moles, Atmospherics.T20C));

            var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid);
            SEntMan.System<GravitySystem>().EnableGravity(map.Grid, gravity);

            var minds = SEntMan.System<SharedMindSystem>();

            theatre.Surgeon = SEntMan.SpawnEntity(HumanProto, map.GridCoords);
            if (!minds.TryGetMind(ServerSession!, out var mind, out _))
                mind = minds.CreateMind(ServerSession!.UserId, "Surgeon").Owner;

            minds.TransferTo(mind, theatre.Surgeon);
            theatre.SurgeonMind = mind;

            theatre.Patient = SEntMan.SpawnEntity(HumanProto, map.GridCoords);
            SEntMan.EnsureComponent<TestListenerComponent>(theatre.Patient);
            SEntMan.System<StandingStateSystem>().Down(theatre.Patient);

            foreach (var tool in FloorTools)
                SEntMan.SpawnEntity(tool, map.GridCoords);
        });

        await RunTicksSync(5);
        return theatre;
    }

    private EntityUid Part(Theatre theatre, ProtoId<OrganCategoryPrototype> category)
    {
        return SEntMan.System<OrganLookupSystem>().EnumerateOrgansOfCategory(theatre.Patient, category).First().Owner;
    }

    private int Points(Theatre theatre)
    {
        return SEntMan.System<FSMedicalStatsSystem>().GetStats(theatre.SurgeonMind).HealingPoints;
    }

    private int Completions(Theatre theatre, string surgery)
    {
        return SEntMan.System<SurgeryCompletedListener>().Count(theatre.Patient,
            ev => SEntMan.GetComponent<MetaDataComponent>(ev.Surgery).EntityPrototype?.ID == surgery);
    }

    private async Task Perform(Theatre theatre, EntityUid part, string surgery, string step)
    {
        await Server.WaitAssertion(() =>
        {
            var started = SEntMan.System<SharedSurgerySystem>()
                .TryDoSurgeryStep(theatre.Patient, part, theatre.Surgeon, surgery, step, out var reason);

            Assert.That(started, Is.True, $"{surgery} refused {step}: {reason}");
        });

        await WaitForTheSurgeon(theatre, step);
    }

    private async Task WaitForTheSurgeon(Theatre theatre, string step)
    {
        for (var i = 0; i < 120; i++)
        {
            await RunSeconds(0.5f);

            var busy = true;
            await Server.WaitPost(() => busy = SEntMan.HasComponent<ActiveDoAfterComponent>(theatre.Surgeon));

            if (!busy)
                return;
        }

        Assert.Fail($"{step} was still running after a minute");
    }

    [Test]
    public async Task OpeningAndClosingAnArmPaysNothing()
    {
        var theatre = await Prepare();
        var arm = EntityUid.Invalid;
        var before = 0;

        await Server.WaitPost(() =>
        {
            arm = Part(theatre, OrganCategories.ArmLeft);
            before = Points(theatre);
        });

        await Perform(theatre, arm, "SurgeryOpenIncision", "SurgeryStepOpenIncisionScalpel");
        await Perform(theatre, arm, "SurgeryOpenIncision", "SurgeryStepRetractSkin");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<SkinRetractedComponent>(arm), Is.True, "the arm was never opened");
            Assert.That(Completions(theatre, "SurgeryOpenIncision"), Is.EqualTo(1),
                "opening the arm did not report a finished operation");
        });

        await Perform(theatre, arm, "SurgeryCloseIncision", "SurgeryStepCloseIncision");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(arm), Is.False, "the arm was never closed");
            Assert.That(Completions(theatre, "SurgeryCloseIncision"), Is.EqualTo(1),
                "closing the arm did not report a finished operation");
            Assert.That(Points(theatre), Is.EqualTo(before),
                "opening and closing a healthy arm paid the surgeon");
        });
    }

    [Test]
    public async Task TendingAWoundPaysTheSurgeonOnce()
    {
        var theatre = await Prepare();
        var arm = EntityUid.Invalid;
        var before = 0;

        await Server.WaitAssertion(() =>
        {
            var wounds = SEntMan.System<WoundSystem>();
            arm = Part(theatre, OrganCategories.ArmLeft);

            Assert.That(wounds.TryInduceWound(arm, Blunt, 6, out _), Is.True, "the arm could not be bruised");

            before = Points(theatre);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var surgery = SEntMan.System<SharedSurgerySystem>();
            Assert.That(surgery.GetSingleton("SurgeryTendWoundsBrute") is { } tend
                        && surgery.SurgeryConditionsHold(theatre.Patient, arm, tend),
                Is.True,
                "the bruised arm is not offered Tend Wounds");
        });

        await Perform(theatre, arm, "SurgeryTendWoundsBrute", "SurgeryStepCarefulIncisionScalpel");
        await Perform(theatre, arm, "SurgeryTendWoundsBrute", "SurgeryStepRepairBruteTissue");

        await Server.WaitAssertion(() =>
        {
            var wounds = SEntMan.System<WoundSystem>();
            Assert.That(wounds.GetWoundableSeverityPoint(arm, damageGroup: "Brute", healable: true).Float(),
                Is.EqualTo(0f),
                "the bruises are still there");
            Assert.That(Completions(theatre, "SurgeryTendWoundsBrute"), Is.EqualTo(1),
                "tending the arm should report one finished operation");
            Assert.That(Points(theatre), Is.GreaterThan(before), "tending a wound paid the surgeon nothing");
        });

        var paid = 0;
        await Server.WaitPost(() => paid = Points(theatre));

        await Perform(theatre, arm, "SurgeryCloseIncision", "SurgeryStepCloseIncision");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(arm), Is.False, "the arm was left open");
            Assert.That(Points(theatre), Is.EqualTo(paid), "closing up after the repair paid a second time");
        });
    }

    [Test]
    public async Task PressingAStepAgainDoesNotCancelIt()
    {
        var theatre = await Prepare();
        var arm = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            arm = Part(theatre, OrganCategories.ArmLeft);
            var surgery = SEntMan.System<SharedSurgerySystem>();

            Assert.That(surgery.TryDoSurgeryStep(theatre.Patient,
                    arm,
                    theatre.Surgeon,
                    "SurgeryOpenIncision",
                    "SurgeryStepOpenIncisionScalpel",
                    out var first),
                Is.True,
                $"the incision was refused: {first}");

            Assert.That(surgery.TryDoSurgeryStep(theatre.Patient,
                    arm,
                    theatre.Surgeon,
                    "SurgeryOpenIncision",
                    "SurgeryStepOpenIncisionScalpel",
                    out var second),
                Is.False,
                "a second press started the same step twice");

            Assert.That(second, Is.EqualTo(StepInvalidReason.DoAfterFailed));
        });

        await WaitForTheSurgeon(theatre, "SurgeryStepOpenIncisionScalpel");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<IncisionOpenComponent>(arm), Is.True,
                "the second press cancelled the incision that was already under way");
        });
    }

    [Test]
    public async Task ReattachingAnArmStaysOfferedUntilItIsSealed()
    {
        var theatre = await Prepare();
        var torso = EntityUid.Invalid;
        var before = 0;

        await Server.WaitAssertion(() =>
        {
            var lookup = SEntMan.System<OrganLookupSystem>();
            var arm = Part(theatre, OrganCategories.ArmLeft);

            Assert.That(lookup.TryGetParentOrgan(arm, out torso), Is.True);
            SEntMan.System<WoundSystem>().AmputateWoundableSafely(torso, arm, amputateChildrenSafely: true);

            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(theatre.Surgeon, arm), Is.True,
                "the surgeon could not pick up the severed arm");

            before = Points(theatre);
        });

        await Perform(theatre, torso, "SurgeryOpenIncision", "SurgeryStepOpenIncisionScalpel");
        await Perform(theatre, torso, "SurgeryOpenIncision", "SurgeryStepRetractSkin");
        await Perform(theatre, torso, "SurgeryAttachLeftArm", "SurgeryStepInsertFeature");

        await Server.WaitAssertion(() =>
        {
            var surgery = SEntMan.System<SharedSurgerySystem>();
            var attach = surgery.GetSingleton("SurgeryAttachLeftArm");

            Assert.That(attach, Is.Not.Null);
            Assert.That(SEntMan.System<OrganLookupSystem>().HasOrganOfCategory(theatre.Patient, OrganCategories.ArmLeft),
                Is.True,
                "the arm did not go back on");
            Assert.That(surgery.SurgeryConditionsHold(theatre.Patient, torso, attach!.Value), Is.True,
                "Attach Left Arm vanished before its wounds were sealed");
            Assert.That(Points(theatre), Is.EqualTo(before), "the surgeon was paid before the arm was sealed");
        });

        await Perform(theatre, torso, "SurgeryAttachLeftArm", "SurgeryStepSealWounds");

        await Server.WaitAssertion(() =>
        {
            Assert.That(Completions(theatre, "SurgeryAttachLeftArm"), Is.EqualTo(1),
                "sealing the arm did not finish the operation");
            Assert.That(Points(theatre), Is.GreaterThan(before), "reattaching an arm paid the surgeon nothing");
        });
    }

    [Test]
    public async Task EveryRefusalHasAMessage()
    {
        await Server.WaitAssertion(() =>
        {
            var surgery = SEntMan.System<SharedSurgerySystem>();

            foreach (var reason in Enum.GetValues<StepInvalidReason>())
            {
                if (reason == StepInvalidReason.None)
                    continue;

                var text = surgery.DescribeRefusal(reason, "SurgeryStepOpenIncisionScalpel", null);

                Assert.That(text, Is.Not.Empty, $"{reason} has no message");
                Assert.That(text, Does.Not.StartWith("fs-surgery-"), $"{reason} shows a raw locale key");
            }
        });
    }

    [Test]
    public async Task TheWindowListsBodyPartsAndOnlyGuidedOperations()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity(HumanProto, map.GridCoords);
            var guide = SEntMan.System<FSSurgeryGuideSystem>();
            var (choices, unavailable) = SEntMan.System<ServerSurgerySystem>().BuildOptions(patient);

            Assert.That(choices, Is.Not.Empty, "a healthy patient has no parts to operate on");

            foreach (var net in choices.Keys.Concat(unavailable.Keys))
            {
                var organ = SEntMan.GetComponent<OrganComponent>(SEntMan.GetEntity(net));
                Assert.That(OrganCategories.ToTarget(organ.Category), Is.Not.Null,
                    $"{organ.Category} is listed as a body part of its own");
            }

            foreach (var id in choices.Values.SelectMany(list => list))
            {
                Assert.That(guide.IsVisible(id), Is.True, $"{id} is offered but the surgery guide hides it");
            }

            foreach (var id in unavailable.Values.SelectMany(list => list))
            {
                var role = guide.RoleOf(id);
                Assert.That(role is FSSurgeryRole.Goal or FSSurgeryRole.Elective, Is.True,
                    $"{id} is listed as unavailable although it is a {role} step");
            }
        });
    }
}
