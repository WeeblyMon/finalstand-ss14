using Content.Shared._FinalStand.Mobs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSWaveDamageScaleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWaveDamageScaleComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnGetMeleeDamage(EntityUid uid, FSWaveDamageScaleComponent comp, ref GetMeleeDamageEvent args)
    {
        if (comp.MeleeDamageMultiplier <= 1f)
            return;
        args.Damage *= comp.MeleeDamageMultiplier;
    }
}
