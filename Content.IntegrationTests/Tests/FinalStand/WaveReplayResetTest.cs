using System.Collections.Generic;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Ammo;
using Content.Server._FinalStand.Emplacement;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Pings;
using Content.Server._FinalStand.Upgrades;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.FinalStand;

// Losing a CCC life sends the round back to prep for the same wave without ending it. The per-wave resupplies and
// allowances have to reset on that path too, or everyone who used theirs during the lost attempt goes without for
// the replay.
[TestFixture]
[TestOf(typeof(WaveAmmoBoxSystem))]
public sealed class WaveReplayResetTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    private static readonly EntProtoId WaveRule = "WaveSurvivalZombie";
    private static readonly EntProtoId AmmoBox = "WaveAmmoBox";
    private static readonly EntProtoId Human = "MobHuman";
    private static readonly EntProtoId Satchel = "FSHarvestSatchel";
    private static readonly EntProtoId Gun = "WeaponPistolViper";
    private static readonly EntProtoId Emplacement = "FSEmplacementCore";

    [Test]
    public async Task LostLifeResetsTheWaveAllowances()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var waves = entMan.System<WaveGameRuleSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var interaction = entMan.System<SharedInteractionSystem>();

        await server.WaitPost(() =>
        {
            Assert.That(ticker.StartGameRule(WaveRule, out _));
            waves.RequestWaveStart();
        });

        await Pair.RunTicksSync(timing.TickRate * 11);

        EntityUid user = default;
        EntityUid box = default;
        EntityUid satchel = default;
        EntityUid gun = default;
        EntityUid core = default;
        HashSet<EntityUid> autoRaised = null;
        await server.WaitAssertion(() =>
        {
            Assert.That(waves.TryGetWaveStatus(out _, out var phase));
            Assert.That(phase, Is.EqualTo(WavePhase.Combat));

            var grid = entMan.EntityQueryEnumerator<MapGridComponent>();
            Assert.That(grid.MoveNext(out var gridUid, out _));
            var coords = new EntityCoordinates(gridUid, 0.5f, 0.5f);

            user = entMan.SpawnEntity(Human, coords);
            box = entMan.SpawnEntity(AmmoBox, coords);
            interaction.InteractHand(user, box);

            // A satchel at its cap, a Battle Trance gun with stacks, a deployed emplacement and a raised repair task.
            satchel = entMan.SpawnEntity(Satchel, coords);
            var harvest = entMan.GetComponent<FSHarvestSatchelComponent>(satchel);
            harvest.AccruedThisWave = harvest.PerWaveCap;

            gun = entMan.SpawnEntity(Gun, coords);
            var trance = entMan.EnsureComponent<FSBattleTranceComponent>(gun);
            var state = entMan.EnsureComponent<FSWeaponUpgradeStateComponent>(gun);
            core = entMan.SpawnEntity(Emplacement, coords);
#pragma warning disable RA0002
            trance.Stacks = 3;
            trance.CurrentBonus = 0.3f;
            state.DamageMultiplier += 0.3f;
            entMan.GetComponent<FSEmplacementComponent>(core).Deployed = true;
#pragma warning restore RA0002

            autoRaised = (HashSet<EntityUid>) typeof(FSTaskBoardSystem)
                .GetField("_autoRaised", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(entMan.System<FSTaskBoardSystem>())!;
            autoRaised.Add(box);
        });

        await Pair.RunTicksSync(timing.TickRate * 5);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<WaveAmmoBoxComponent>(box);
            Assert.That(comp.UsedBy, Does.Contain(user), "the refill didn't go through");

            Assert.That(waves.FailWave(), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(comp.UsedBy, Is.Empty, "the replayed wave kept last attempt's ammo box use");
                Assert.That(entMan.GetComponent<FSHarvestSatchelComponent>(satchel).AccruedThisWave, Is.Zero,
                    "the replayed wave kept last attempt's harvest cap");
                Assert.That(entMan.GetComponent<FSBattleTranceComponent>(gun).Stacks, Is.Zero,
                    "the replayed wave kept last attempt's Battle Trance stacks");
                Assert.That(entMan.GetComponent<FSWeaponUpgradeStateComponent>(gun).DamageMultiplier, Is.EqualTo(1f).Within(1e-4f),
                    "Battle Trance's bonus outlived its stacks");
                Assert.That(!entMan.EntityExists(core) || entMan.GetComponent<FSEmplacementComponent>(core).Packed,
                    "the emplacement stayed deployed into the replay's prep");
                Assert.That(autoRaised, Is.Empty, "the replayed wave can't raise repair tasks raised last attempt");
            });
        });
    }
}
