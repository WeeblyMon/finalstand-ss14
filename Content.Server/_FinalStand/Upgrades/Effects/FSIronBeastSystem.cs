using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class FSIronBeastSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

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

        if (damageable.TotalDamage * 2 < deadThreshold!.Value)
            return;

        // Clamped: research stacks ResistBonus on top of the base 20%, and a total at or above
        // 1.0 would flip the multiplier negative and heal the wielder on every hit.
        var resist = Math.Clamp(0.2f + buff.ResistBonus, 0f, 0.95f);
        args.Damage *= 1f - resist;
    }
}
