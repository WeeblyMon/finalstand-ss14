using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class FSOverclockedSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSOverclockedComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<FSOverclockedComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSOverclockedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Spool <= 0f)
                continue;
            if (curTime - comp.LastShotTime < FSOverclockedComponent.SpoolDecayDelay)
                continue;

            comp.Spool = Math.Max(0f, comp.Spool - FSOverclockedComponent.SpoolDecayRate * frameTime);
            Dirty(uid, comp);
            _gun.RefreshModifiers(uid);
        }
    }

    private void OnShot(EntityUid uid, FSOverclockedComponent comp, ref GunShotEvent args)
    {
        comp.LastShotTime = _timing.CurTime.TotalSeconds;
        comp.Spool = Math.Min(1f, comp.Spool + FSOverclockedComponent.SpoolGainPerShot);
        Dirty(uid, comp);
        _gun.RefreshModifiers(uid);
    }

    private void OnRefreshModifiers(EntityUid uid, FSOverclockedComponent comp, ref GunRefreshModifiersEvent args)
    {
        args.FireRate += comp.Spool * comp.Level * FSOverclockedComponent.FireRateBonusPerLevel * comp.ResearchRampMultiplier;
    }
}
