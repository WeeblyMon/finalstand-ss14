using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.RiotShield;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSStunningStrikesSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSStunOverrideSystem _stun = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPlayerMeleeHitEvent>(OnPlayerMeleeHit);
    }

    private void OnPlayerMeleeHit(ref FSPlayerMeleeHitEvent ev)
    {
        var hit = ev.Hit;
        if (!HasComp<FSRiotShieldComponent>(hit.Weapon)
            || !_mind.TryGetMind(hit.User, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        var level = perks.GetSlottedLevel("StunningStrikes");
        if (level <= 0)
            return;

        foreach (var target in hit.HitEntities)
        {
            if (HasComp<WaveSpawnedTagComponent>(target)
                && _mobState.IsAlive(target)
                && _random.Prob(level * FSPerkBonusConstants.StunningStrikesChancePerLevel))
                TryStun(target);
        }
    }

    public bool TryStun(EntityUid zombie)
    {
        var seconds = FSPerkBonusConstants.StunningStrikesSeconds;
        if (HasComp<FSSpecialZombieComponent>(zombie))
        {
            var cooldown = EnsureComp<FSStunningStrikesCooldownComponent>(zombie);
            if (_timing.CurTime < cooldown.NextAllowed)
                return false;

            cooldown.NextAllowed = _timing.CurTime + TimeSpan.FromSeconds(FSPerkBonusConstants.StunningStrikesSpecialCooldown);
            seconds *= FSPerkBonusConstants.StunningStrikesSpecialFactor;
        }

        if (TryComp<FSStunResistComponent>(zombie, out var resist))
            seconds *= resist.DurationMultiplier;

        return _stun.TryForceStun(zombie, TimeSpan.FromSeconds(seconds), visualized: true);
    }
}

[RegisterComponent]
public sealed partial class FSStunningStrikesCooldownComponent : Component
{
    public TimeSpan NextAllowed;
}
