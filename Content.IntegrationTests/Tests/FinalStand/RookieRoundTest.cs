using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Mentor;
using Content.Server._FinalStand.Onboarding;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class RookieRoundTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    private ICommonSession _veteran = default!;
    private ICommonSession _rookie = default!;

    private async Task StartRoundWithARookie()
    {
        var ticker = SEntMan.System<GameTicker>();
        var store = SEntMan.System<FSPlayerDataStore>();
        var playTime = Server.ResolveDependency<PlayTimeTrackingManager>();

        _rookie = await Server.AddDummySession("Rookie");
        await Pair.RunTicksSync(5);
        _veteran = ServerSession!;

        await Server.WaitPost(() =>
        {
            foreach (var session in new[] { _veteran, _rookie })
            {
                store.DeleteProgress(session.UserId.UserId);
                store.UpsertPerkPoints(session.UserId.UserId, session.Name, 0);
            }

            playTime.AddTimeToOverallPlaytime(_veteran, FSMentorRules.MinPlaytime);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(_veteran.AttachedEntity, Is.Not.Null, "the veteran spawned into the round");
            Assert.That(_rookie.AttachedEntity, Is.Not.Null, "the rookie spawned into the round");
            Assert.That(playTime.GetOverallPlaytime(_rookie), Is.LessThan(FSRookieRules.RookieThreshold),
                "test precondition: the dummy player is new");
        });
    }

    private EntityUid MindOf(ICommonSession session)
    {
        Assert.That(SEntMan.System<SharedMindSystem>().TryGetMind(session, out var mindId, out _), Is.True);
        return mindId;
    }

    private EntityUid BodyOf(ICommonSession session) => session.AttachedEntity!.Value;

    private void MakeVeteranAMentor()
    {
        var level = SEntMan.GetComponent<FSPlayerLevelComponent>(MindOf(_veteran));
#pragma warning disable RA0002
        level.Level = FSMentorRules.MinLevel;
#pragma warning restore RA0002
        Assert.That(SEntMan.System<FSMentorSystem>().CanMentor(_veteran, _rookie), Is.True, "test precondition");
    }

    private int MentorPoints() => SEntMan.System<FSMentorSystem>().CurrentRecord(_veteran.UserId.UserId).Total;

    private int Credits(ICommonSession session) => SEntMan.GetComponent<FSPlayerWalletComponent>(MindOf(session)).Credits;

    private void Damage(EntityUid target, float amount, EntityUid? origin)
    {
        var damage = new DamageSpecifier(SProtoMan.Index(Blunt), amount);
        Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(target, damage, ignoreResistances: true, origin: origin), Is.True);
    }

    private void EndWave(int wave)
    {
        var ended = new WaveEndedEvent(wave);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ended);
    }

    private void Place(EntityUid body, Vector2 offset)
    {
        var xform = SEntMan.System<SharedTransformSystem>();
        var anchor = BodyOf(_veteran);
        xform.SetCoordinates(body, xform.GetMoverCoordinates(anchor).Offset(offset));
    }

    [Test]
    public async Task SpawningIntoARoundHandsOutTheRookiePackage()
    {
        await StartRoundWithARookie();

        await Server.WaitAssertion(() =>
        {
            var rookieMind = MindOf(_rookie);
            var veteranMind = MindOf(_veteran);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<FSPlayerWalletComponent>(rookieMind).PerkPoints,
                    Is.EqualTo(FSRookieRules.StarterPerkPoints));
                Assert.That(SEntMan.GetComponent<FSPlayerLevelComponent>(rookieMind).CatchUpMultiplier,
                    Is.EqualTo(FSRookieRules.CatchUpXpMultiplier));
                Assert.That(SEntMan.GetComponent<FSPlayerBadgeComponent>(BodyOf(_rookie)).Rookie, Is.True);
                Assert.That(SEntMan.System<FSPlayerDataStore>().GetOnboarding(_rookie.UserId.UserId).StarterGranted, Is.True);

                Assert.That(SEntMan.GetComponent<FSPlayerWalletComponent>(veteranMind).PerkPoints, Is.Zero,
                    "a veteran seen for the first time gets no starter points");
                Assert.That(SEntMan.GetComponent<FSPlayerLevelComponent>(veteranMind).CatchUpMultiplier, Is.EqualTo(1f));
                Assert.That(SEntMan.GetComponent<FSPlayerBadgeComponent>(BodyOf(_veteran)).Rookie, Is.False);
            });
        });
    }

    [Test]
    public async Task HealingARookiePaysAPointPerFiftyHealed()
    {
        await StartRoundWithARookie();

        await Server.WaitAssertion(() =>
        {
            MakeVeteranAMentor();
            var credits = Credits(_veteran);

            Damage(BodyOf(_rookie), 60, null);
            Damage(BodyOf(_rookie), -60, BodyOf(_veteran));
            Assert.That(MentorPoints(), Is.EqualTo(1));
            Assert.That(Credits(_veteran) - credits, Is.AtLeast(FSMentorSystem.HealCreditsPerMp), "the mentor bonus rides on top of the medic payout");

            Damage(BodyOf(_rookie), 40, null);
            Damage(BodyOf(_rookie), -40, BodyOf(_veteran));
            Assert.That(MentorPoints(), Is.EqualTo(2), "the 10 left over from the first heal carries into the next");
        });
    }

    [Test]
    public async Task RevivingARookiePaysOncePerCooldown()
    {
        await StartRoundWithARookie();

        await Server.WaitAssertion(() =>
        {
            MakeVeteranAMentor();
            var mobState = SEntMan.System<MobStateSystem>();
            var rookie = BodyOf(_rookie);
            var credits = Credits(_veteran);

            mobState.ChangeMobState(rookie, MobState.Dead);
            mobState.ChangeMobState(rookie, MobState.Critical, origin: BodyOf(_veteran));
            Assert.That(MentorPoints(), Is.EqualTo(FSMentorSystem.ReviveMp));
            Assert.That(Credits(_veteran) - credits, Is.AtLeast(FSMentorSystem.ReviveCredits));

            mobState.ChangeMobState(rookie, MobState.Dead);
            mobState.ChangeMobState(rookie, MobState.Critical, origin: BodyOf(_veteran));
            Assert.That(MentorPoints(), Is.EqualTo(FSMentorSystem.ReviveMp), "kill-and-revive loops don't farm points");
        });
    }

    [Test]
    public async Task ARevivePaysTheMentorPastTheMedicPayoutCap()
    {
        await StartRoundWithARookie();

        await Server.WaitAssertion(() =>
        {
            MakeVeteranAMentor();
            var mobState = SEntMan.System<MobStateSystem>();
            var rookie = BodyOf(_rookie);

            // Three stabilises use up this wave's medic payouts for the rookie, and pay no mentor points.
            for (var i = 0; i < 3; i++)
            {
                mobState.ChangeMobState(rookie, MobState.Critical);
                mobState.ChangeMobState(rookie, MobState.Alive, origin: BodyOf(_veteran));
            }
            Assert.That(MentorPoints(), Is.Zero, "test precondition");

            mobState.ChangeMobState(rookie, MobState.Dead);
            mobState.ChangeMobState(rookie, MobState.Critical, origin: BodyOf(_veteran));
            Assert.That(MentorPoints(), Is.EqualTo(FSMentorSystem.ReviveMp),
                "the mentor's own cooldown limits revives, not the medic payout cap");
        });
    }

    [Test]
    public async Task HurtingARookieVoidsThePairForTheRound()
    {
        await StartRoundWithARookie();

        await Server.WaitAssertion(() =>
        {
            MakeVeteranAMentor();
            Damage(BodyOf(_rookie), 60, BodyOf(_veteran));
            Damage(BodyOf(_rookie), -60, BodyOf(_veteran));
            Assert.That(MentorPoints(), Is.Zero, "healing back damage you dealt earns nothing");

            EndWave(1);
            Assert.That(MentorPoints(), Is.Zero);
        });

        await Server.WaitAssertion(() =>
        {
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(SEntMan.System<FSMentorSystem>().Award(_veteran, _rookie, 1, "fs-mentor-reason-escort"), Is.EqualTo(1),
                "the pair is only voided for the round it happened in");
        });
    }

    [Test]
    public async Task StayingCloseToARookieThroughAWavePaysAPoint()
    {
        await StartRoundWithARookie();

        await Server.WaitAssertion(() =>
        {
            MakeVeteranAMentor();
            Place(BodyOf(_rookie), new Vector2(2, 0));
            EndWave(1);
            Assert.That(MentorPoints(), Is.EqualTo(FSMentorSystem.WaveEscortMp));

            Place(BodyOf(_rookie), new Vector2(FSMentorSystem.WaveEscortRange + 4, 0));
            EndWave(2);
            Assert.That(MentorPoints(), Is.EqualTo(FSMentorSystem.WaveEscortMp), "too far away to count");

            Place(BodyOf(_rookie), new Vector2(2, 0));
            SEntMan.System<MobStateSystem>().ChangeMobState(BodyOf(_veteran), MobState.Critical);
            EndWave(3);
            Assert.That(MentorPoints(), Is.EqualTo(FSMentorSystem.WaveEscortMp), "a downed mentor isn't escorting anyone");
        });
    }

    [Test]
    public async Task ARookiesFirstRoundPaysTheMentorWhoWasThere()
    {
        await StartRoundWithARookie();

        await Server.WaitAssertion(() =>
        {
            MakeVeteranAMentor();
            Place(BodyOf(_rookie), new Vector2(FSMentorSystem.WaveEscortRange + 4, 0));
            for (var wave = 1; wave <= FSRookieRules.FirstRoundMinWaves; wave++)
                EndWave(wave);
            Assert.That(MentorPoints(), Is.Zero, "test precondition: no escort points");

            var onboarding = SEntMan.System<FSOnboardingSystem>();
            Assert.That(onboarding.TryPayFirstRound(_rookie, MindOf(_rookie)), Is.True);
            Assert.That(MentorPoints(), Is.EqualTo(FSMentorSystem.FirstRoundMp));
        });
    }
}
