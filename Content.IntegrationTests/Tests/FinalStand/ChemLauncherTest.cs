#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class ChemLauncherTest : GameTest
{
    private const string HumanProto = "MobHuman";
    private const string EnemyProto = "FSZombieNormal";
    private const string LauncherProto = "FSChemLauncher";
    private const string CauldronProto = "FSTonicCauldron";

    private static readonly ProtoId<ReagentPrototype> Stim = "FSCombatStim";
    private static readonly ProtoId<ReagentPrototype> Corrosive = "FSCorrosive";

    [Test]
    public async Task AShotSpendsTheFormulasCost()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shared = entMan.System<FSChemLauncherSharedSystem>();
            var (uid, launcher) = SpawnLauncher(entMan, map.GridCoords);
            Assert.That(shared.TryGetActive(launcher, out var formula), Is.True, "the launcher spawns with nothing loaded");

            var full = launcher.Reserve;
            var ammo = Shoot(entMan, uid, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(ammo, Has.Count.EqualTo(1));
                Assert.That(launcher.Reserve, Is.EqualTo(full - formula!.Cost).Within(0.01f));
                Assert.That(entMan.TryGetComponent<FSChemCanisterComponent>(ammo[0].Entity, out var canister), Is.True,
                    "the shot is not a canister, so it lands without a cloud");
                Assert.That(canister!.Formula.Id, Is.EqualTo(formula.ID));
                Assert.That(canister.Launcher, Is.EqualTo(uid), "the cloud must know which launcher to credit");
            });

            var count = new GetAmmoCountEvent();
            entMan.EventBus.RaiseLocalEvent(uid, ref count);

            Assert.That(count.Count, Is.EqualTo((int) (launcher.Reserve / formula.Cost)),
                "the ammo counter must show shots left of the selected formula");
        });
    }

    [Test]
    public async Task ADryLauncherFiresNothing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var (uid, launcher) = SpawnLauncher(entMan, map.GridCoords);
            launcher.Reserve = 1f;

            var ev = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), map.GridCoords, null);
            entMan.EventBus.RaiseLocalEvent(uid, ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Ammo, Is.Empty);
                Assert.That(ev.Reason, Is.Not.Null, "a dry click must say why");
                Assert.That(launcher.Reserve, Is.EqualTo(1f));
            });
        });
    }

    [Test]
    public async Task SupportCloudsBuffCrewAndNotZombies()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid crew = default;
        EntityUid zombie = default;

        await server.WaitAssertion(() =>
        {
            crew = SpawnCrew(entMan, map.GridCoords);
            zombie = entMan.SpawnEntity(EnemyProto, map.GridCoords);

            Assert.That(SpawnZone(entMan, map.MapCoords, Stim, null, null), Is.Not.Null);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(bonus.GetBonus(crew, FSMedicalBonusCategory.Damage), Is.GreaterThan(0f),
                    "standing in a stim cloud must buff the crewmate");
                Assert.That(bonus.GetBonus(zombie, FSMedicalBonusCategory.Damage), Is.EqualTo(0f),
                    "a support cloud must never strengthen the horde");
            });
        });
    }

    [Test]
    public async Task HostileCloudsHurtZombiesAndNotCrew()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid crew = default;
        EntityUid zombie = default;

        await server.WaitAssertion(() =>
        {
            crew = SpawnCrew(entMan, map.GridCoords);
            zombie = entMan.SpawnEntity(EnemyProto, map.GridCoords);

            Assert.That(SpawnZone(entMan, map.MapCoords, Corrosive, null, null), Is.Not.Null);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Caustic(entMan, zombie), Is.GreaterThan(0f), "corrosive must burn the horde");
                Assert.That(Caustic(entMan, crew), Is.EqualTo(0f), "a hostile cloud must never touch crew");
            });
        });
    }

    [Test]
    public async Task CloudDamageIsTheChemists()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid chemist = default;
        EntityUid chemistMind = default;
        EntityUid zombie = default;
        var before = 0;

        await server.WaitAssertion(() =>
        {
            var wallet = entMan.System<FSPlayerWalletSystem>();

            chemist = SpawnChemist(entMan, map.GridCoords, out chemistMind);
            zombie = entMan.SpawnEntity(EnemyProto, map.GridCoords);
            before = wallet.GetCredits(chemistMind);

            SpawnZone(entMan, map.MapCoords, Corrosive, chemist, null);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var wallet = entMan.System<FSPlayerWalletSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(entMan.TryGetComponent<FSChemTaggedComponent>(zombie, out var tag), Is.True,
                    "the zombie must remember whose cloud burned it");
                Assert.That(tag!.Chemist, Is.EqualTo(chemist));
                Assert.That(tag.Damage, Is.GreaterThan(0f));
                Assert.That(wallet.GetCredits(chemistMind), Is.GreaterThan(before),
                    "cloud damage earns hit money like any gun");
            });
        });
    }

    [Test]
    public async Task ABuffDoesNotScaleWithTheDose()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var reactive = entMan.System<ReactiveSystem>();

            var small = SpawnCrew(entMan, map.GridCoords);
            var large = SpawnCrew(entMan, map.GridCoords);

            reactive.DoEntityReaction(small, new Solution(Stim, 1), ReactionMethod.Touch);
            reactive.DoEntityReaction(large, new Solution(Stim, 50), ReactionMethod.Touch);

            Assert.That(bonus.TryGetBuff(large, "chem-stim", out var buff), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(bonus.GetBonus(large, FSMedicalBonusCategory.Damage),
                    Is.EqualTo(bonus.GetBonus(small, FSMedicalBonusCategory.Damage)).Within(0.001f),
                    "a big splash must not stack the bonus up to the cap");
                Assert.That(buff!.EndTime!.Value - timing.CurTime, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(20)),
                    "a big splash must not last for minutes");
            });
        });
    }

    [Test]
    public async Task BuffingCrewmatesBuildsCatalyst()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        FSChemLauncherComponent launcher = default!;

        await server.WaitAssertion(() =>
        {
            var chemist = SpawnChemist(entMan, map.GridCoords, out _);
            SpawnCrew(entMan, map.GridCoords);

            EntityUid uid;
            (uid, launcher) = SpawnLauncher(entMan, map.GridCoords);
            launcher.Catalyst = 0f;

            SpawnZone(entMan, map.MapCoords, Stim, chemist, uid);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(launcher.Catalyst, Is.GreaterThan(0f), "dosing a crewmate must charge the catalyst");
        });
    }

    [Test]
    public async Task AnArmedCatalystIsSpentOnTheNextShot()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var (uid, launcher) = SpawnLauncher(entMan, map.GridCoords);
            launcher.Catalyst = FSChemLauncherSystem.FullCatalyst;
            launcher.CatalystArmed = true;

            var first = Shoot(entMan, uid, map.GridCoords);
            var second = Shoot(entMan, uid, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<FSChemCanisterComponent>(first[0].Entity!.Value).Catalysed, Is.True,
                    "the armed shot must be catalysed");
                Assert.That(entMan.GetComponent<FSChemCanisterComponent>(second[0].Entity!.Value).Catalysed, Is.False,
                    "only one shot per charge");
                Assert.That(launcher.CatalystArmed, Is.False);
                Assert.That(launcher.Catalyst, Is.EqualTo(0f));
            });
        });
    }

    [Test]
    public async Task ACatalysedCloudIsBiggerAndLasts()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var zones = entMan.System<FSChemZoneSystem>();

            var plain = zones.SpawnZone(map.MapCoords, [Stim], null, null, false, null);
            var catalysed = zones.SpawnZone(map.MapCoords, [Stim], null, null, true, null);

            var a = entMan.GetComponent<FSChemZoneComponent>(plain!.Value);
            var b = entMan.GetComponent<FSChemZoneComponent>(catalysed!.Value);

            Assert.Multiple(() =>
            {
                Assert.That(b.Radius, Is.GreaterThan(a.Radius));
                Assert.That(b.Lifetime, Is.GreaterThan(a.Lifetime));
                Assert.That(b.Scale, Is.GreaterThan(a.Scale));
            });
        });
    }

    [Test]
    public async Task EachCrewmateDrinksTheTonicOnce()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var tonic = entMan.System<FSTonicCauldronSystem>();
            var bonus = entMan.System<FSMedicalBonusSystem>();
            var wallet = entMan.System<FSPlayerWalletSystem>();

            var chemist = SpawnChemist(entMan, map.GridCoords, out var chemistMind);
            var drinker = SpawnCrew(entMan, map.GridCoords);

            Assert.That(tonic.TryBrew(chemist, map.GridCoords, CauldronProto), Is.True);
            Assert.That(tonic.TryBrew(chemist, map.GridCoords, CauldronProto), Is.False,
                "one tonic per chemist per wave");

            var cauldron = FindCauldron(entMan, chemist);
            Assert.That(cauldron, Is.Not.Null);

            var before = wallet.GetCredits(chemistMind);
            var pay = entMan.GetComponent<FSTonicCauldronComponent>(cauldron!.Value).BrewerPay;

            for (var i = 0; i < 3; i++)
                entMan.EventBus.RaiseLocalEvent(cauldron.Value, new InteractHandEvent(drinker, cauldron.Value));

            Assert.Multiple(() =>
            {
                Assert.That(bonus.HasBuff(drinker, FSTonicCauldronComponent.Source), Is.True);
                Assert.That(wallet.GetCredits(chemistMind) - before, Is.EqualTo(pay),
                    "drinking again must not pay the brewer again");
            });

            var ended = new WaveEndedEvent(1);
            entMan.EventBus.RaiseEvent(EventSource.Local, ref ended);

            Assert.That(bonus.HasBuff(drinker, FSTonicCauldronComponent.Source), Is.False,
                "the tonic lasts one wave");
        });
    }

    private static (EntityUid, FSChemLauncherComponent) SpawnLauncher(IEntityManager entMan, EntityCoordinates coords)
    {
        var uid = entMan.SpawnEntity(LauncherProto, coords);
        return (uid, entMan.GetComponent<FSChemLauncherComponent>(uid));
    }

    private static List<(EntityUid? Entity, IShootable Shootable)> Shoot(IEntityManager entMan, EntityUid launcher, EntityCoordinates coords)
    {
        var ev = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coords, null);
        entMan.EventBus.RaiseLocalEvent(launcher, ev);
        return ev.Ammo;
    }

    private static EntityUid? SpawnZone(IEntityManager entMan, MapCoordinates coords, ProtoId<ReagentPrototype> reagent,
        EntityUid? chemist, EntityUid? launcher)
    {
        return entMan.System<FSChemZoneSystem>().SpawnZone(coords, [reagent], chemist, launcher, false, null);
    }

    private static EntityUid SpawnCrew(IEntityManager entMan, EntityCoordinates coords)
    {
        var crew = entMan.SpawnEntity(HumanProto, coords);
        entMan.EnsureComponent<FSFriendlyFireComponent>(crew);
        return crew;
    }

    private static EntityUid SpawnChemist(IEntityManager entMan, EntityCoordinates coords, out EntityUid mind)
    {
        var minds = entMan.System<SharedMindSystem>();

        var chemist = SpawnCrew(entMan, coords);
        mind = minds.CreateMind(null, "Chemist");
        minds.TransferTo(mind, chemist, mind: entMan.GetComponent<MindComponent>(mind));

        return chemist;
    }

    private static float Caustic(IEntityManager entMan, EntityUid uid)
    {
        return entMan.GetComponent<DamageableComponent>(uid).Damage.DamageDict.GetValueOrDefault("Caustic").Float();
    }

    private static EntityUid? FindCauldron(IEntityManager entMan, EntityUid brewer)
    {
        var query = entMan.EntityQueryEnumerator<FSTonicCauldronComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Brewer == brewer)
                return uid;
        }

        return null;
    }
}
