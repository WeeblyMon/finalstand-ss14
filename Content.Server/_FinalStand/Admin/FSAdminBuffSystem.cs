using Content.Shared._FinalStand.Admin;
using Content.Shared.Damage.Systems;

namespace Content.Server._FinalStand.Admin;

public sealed class FSAdminBuffSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAdminBuffComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, FSAdminBuffComponent comp, DamageModifyEvent args)
    {
        args.Damage *= comp.DamageTakenMultiplier;
    }
}
