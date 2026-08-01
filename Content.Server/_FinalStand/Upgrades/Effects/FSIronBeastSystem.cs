using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class FSIronBeastSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSIronBeastComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<FSIronBeastBuffComponent, DamageModifyEvent>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<FSIronBeastBuffComponent>();
        while (query.MoveNext(out var uid, out var buff))
        {
            if (curTime - buff.LastFireTime > FSIronBeastBuffComponent.FireTimeout)
                RemCompDeferred<FSIronBeastBuffComponent>(uid);
        }
    }

    private void OnShot(EntityUid uid, FSIronBeastComponent comp, ref GunShotEvent args)
    {
        var buff = EnsureComp<FSIronBeastBuffComponent>(args.User);
        buff.LastFireTime = _timing.CurTime.TotalSeconds;
        buff.ResistBonus = comp.ResistBonus;
    }

    private void OnDamageModify(EntityUid uid, FSIronBeastBuffComponent buff, DamageModifyEvent args)
    {
        if (!_thresholds.TryGetDeadThreshold(uid, out var deadThreshold))
            return;
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        // Only active when below 50% health (taken more than half of max damage)
        if (damageable.TotalDamage * 2 < deadThreshold!.Value)
            return;

        args.Damage *= 1f - (0.2f + buff.ResistBonus);
    }
}
