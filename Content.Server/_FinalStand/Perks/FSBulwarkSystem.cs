using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.RiotShield;
using Content.Server._FinalStand.RiotShield;
using Content.Shared.Blocking.Components;
using Content.Shared.Mind;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSBulwarkSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSAggravatorSystem _aggravator = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan TauntInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextTaunt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    public float WearFactor(EntityUid? wielder)
    {
        var level = LevelOf(wielder);
        return level > 0 ? 1f / (1f + level * FSPerkBonusConstants.BulwarkDurabilityPerLevel) : 1f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTaunt)
            return;
        _nextTaunt = now + TauntInterval;

        var query = EntityQueryEnumerator<FSRiotShieldComponent, BlockingComponent>();
        while (query.MoveNext(out _, out var shield, out var blocking))
        {
            if (blocking.IsRaised && !shield.IsBroken && blocking.User is { } user && LevelOf(user) > 0)
                _aggravator.TauntAround(user, FSPerkBonusConstants.BulwarkTauntRadius);
        }
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Bulwark");
        if (level <= 0 || !ev.WasMeleeKill
            || !TryComp<FSRiotShieldUserComponent>(ev.Killer, out var marker)
            || !TryComp<FSRiotShieldComponent>(marker.Shield, out var shield))
            return;

        var max = shield.BaseDurability * shield.DurabilityMultiplier;
        shield.CurrentDurability = MathF.Min(max,
            shield.CurrentDurability + max * level * FSPerkBonusConstants.BulwarkRepairPerKillPerLevel);
        shield.IsBroken = shield.CurrentDurability <= 0f;
        Dirty(marker.Shield, shield);
    }

    private int LevelOf(EntityUid? wielder)
    {
        return wielder is { } body
               && _mind.TryGetMind(body, out var mindId, out _)
               && TryComp<FSPerkLevelsComponent>(mindId, out var perks)
            ? perks.GetSlottedLevel("Bulwark")
            : 0;
    }
}
