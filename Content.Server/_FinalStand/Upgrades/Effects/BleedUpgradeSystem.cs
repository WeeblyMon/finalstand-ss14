using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

// slash DOT that refreshes on each hit; ticks every 0.5s so damage numbers show real values
public sealed partial class BleedUpgradeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private const string TourniquetContainerId = "Tourniquet";

    private readonly List<EntityUid> _expired = new();

    private static readonly TimeSpan BleedDuration = TimeSpan.FromSeconds(3);
    private const float BleedScalePerLevel = 0.2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        _expired.Clear();
        var query = EntityQueryEnumerator<FSBleedingComponent>();
        while (query.MoveNext(out var uid, out var bleed))
        {
            if (bleed.ExpiresAt <= now)
            {
                _expired.Add(uid);
                continue;
            }

            if (now < bleed.NextTickAt)
                continue;
            bleed.NextTickAt = now + TimeSpan.FromSeconds(0.5);

            if (_container.TryGetContainer(uid, TourniquetContainerId, out var tqSlot) && tqSlot.ContainedEntities.Count > 0)
                continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Slash"] = FixedPoint2.New(bleed.DamagePerSecond * 0.5f);
            _damageable.TryChangeDamage(uid, dmg, ignoreResistances: false, origin: bleed.Instigator);
        }
        foreach (var uid in _expired)
            RemComp<FSBleedingComponent>(uid);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || state.BleedLevel <= 0)
            return;

        var dps = ev.Damage.GetTotal().Float() * BleedScalePerLevel * state.BleedLevel;
        if (dps <= 0f)
            return;

        var bleed = EnsureComp<FSBleedingComponent>(ev.Target);
        bleed.DamagePerSecond = dps;
        bleed.ExpiresAt = _timing.CurTime + BleedDuration;
        bleed.NextTickAt = _timing.CurTime; // tick immediately on first hit
        bleed.Instigator = ev.Shooter;
    }
}
