using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Zombies;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class VaporiseUpgradeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || !state.VaporiseWeakMobEnabled)
            return;
        if (!HasComp<FSVaporiseWeakComponent>(ev.Target))
            return;

        ev.AdditionalMultiplier *= 10000f;
    }
}
