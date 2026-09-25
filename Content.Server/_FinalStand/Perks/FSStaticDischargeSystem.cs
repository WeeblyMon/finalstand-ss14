// Static Discharge perk: a zombie hit stuns every living zombie around the player, on a cooldown.
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed class FSStaticDischargeSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSStunOverrideSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _nearby = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSIncomingDamageModifyEvent>(OnIncomingDamage);
    }

    private void OnIncomingDamage(ref FSIncomingDamageModifyEvent ev)
    {
        if (ev.Args.Origin is not { } attacker
            || !HasComp<WaveSpawnedTagComponent>(attacker)
            || ev.Args.Damage.GetTotal() <= 0
            || !_mind.TryGetMind(ev.Target, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        var level = perks.GetSlottedLevel("StaticDischarge");
        if (level <= 0)
            return;

        var discharge = EnsureComp<FSStaticDischargeComponent>(mindId);
        var now = _timing.CurTime;
        if (now < discharge.NextReady)
            return;

        discharge.NextReady = now + TimeSpan.FromSeconds(FSPerkBonusConstants.StaticDischargeCooldown[level - 1]);

        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(ev.Target).Coordinates, FSPerkBonusConstants.StaticDischargeRadius[level - 1], _nearby);

        foreach (var zombie in _nearby)
        {
            if (!_mobState.IsAlive(zombie))
                continue;

            var seconds = FSPerkBonusConstants.StaticDischargeStunSeconds;
            if (TryComp<FSStunResistComponent>(zombie, out var resist))
                seconds *= resist.DurationMultiplier;
            if (HasComp<FSSpecialZombieComponent>(zombie))
                seconds *= FSPerkBonusConstants.StaticDischargeSpecialFactor;

            _stun.TryForceStun(zombie, TimeSpan.FromSeconds(seconds), visualized: true);
        }
    }
}
