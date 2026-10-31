using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.RiotShield;
using Content.Shared.Blocking.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSBeaconOfLightSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color LightColor = Color.FromHex("#FFE9A8");
    private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextPulse;

    private readonly HashSet<Entity<MindContainerComponent>> _nearby = [];
    private readonly HashSet<EntityUid> _lit = [];
    private readonly HashSet<EntityUid> _wasLit = [];

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextPulse)
            return;
        _nextPulse = now + PulseInterval;

        _wasLit.Clear();
        _wasLit.UnionWith(_lit);
        _lit.Clear();

        var query = EntityQueryEnumerator<FSRiotShieldComponent, BlockingComponent>();
        while (query.MoveNext(out var shield, out var comp, out var blocking))
        {
            if (!blocking.IsRaised || comp.IsBroken || blocking.User is not { } user)
                continue;

            var level = LevelOf(user);
            if (level <= 0)
                continue;

            var radius = FSPerkBonusConstants.BeaconOfLightRadius[level - 1];
            SetLight(shield, radius, true);
            _lit.Add(shield);
            Pulse(user, radius, level * FSPerkBonusConstants.BeaconOfLightHealPerLevel);
        }

        foreach (var shield in _wasLit)
        {
            if (!_lit.Contains(shield) && !TerminatingOrDeleted(shield))
                SetLight(shield, 0f, false);
        }
    }

    private void Pulse(EntityUid user, float radius, float heal)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(user).Coordinates, radius, _nearby);

        foreach (var ally in _nearby)
        {
            if (!ally.Comp.HasMind || HasComp<WaveSpawnedTagComponent>(ally) || !_mobState.IsAlive(ally))
                continue;

            _ailments.StopBleeding(ally, user, FSCureKind.Mass);
            _damageable.HealEvenly(ally.Owner, FixedPoint2.New(-heal));
        }
    }

    private void SetLight(EntityUid shield, float radius, bool on)
    {
        var light = _lights.EnsureLight(shield);
        _lights.SetEnabled(shield, on, light);
        if (!on)
            return;

        _lights.SetColor(shield, LightColor, light);
        _lights.SetRadius(shield, radius + 1f, light);
        _lights.SetEnergy(shield, 1.5f, light);
    }

    private int LevelOf(EntityUid user)
    {
        return _mind.TryGetMind(user, out var mindId, out _) && TryComp<FSPerkLevelsComponent>(mindId, out var perks)
            ? perks.GetSlottedLevel("BeaconOfLight")
            : 0;
    }
}
