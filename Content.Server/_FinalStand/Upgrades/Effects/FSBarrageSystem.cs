using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class FSBarrageSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBarrageComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<FSBarrageComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSBarrageComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Spool <= 0f)
                continue;
            if (curTime - comp.LastShotTime < FSBarrageComponent.SpoolDecayDelay)
                continue;

            comp.Spool = Math.Max(0f, comp.Spool - FSBarrageComponent.SpoolDecayRate * frameTime);
            Dirty(uid, comp);
            _gun.RefreshModifiers(uid);
        }
    }

    private void OnShot(EntityUid uid, FSBarrageComponent comp, ref GunShotEvent args)
    {
        comp.LastShotTime = _timing.CurTime.TotalSeconds;
        comp.Spool = Math.Min(1f, comp.Spool + FSBarrageComponent.SpoolGainPerShot);
        Dirty(uid, comp);
        _gun.RefreshModifiers(uid);
    }

    private void OnRefreshModifiers(EntityUid uid, FSBarrageComponent comp, ref GunRefreshModifiersEvent args)
    {
        args.FireRate += comp.Spool * comp.Level * FSBarrageComponent.FireRateBonusPerLevel;
    }

}
