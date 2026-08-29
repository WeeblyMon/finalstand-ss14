using Content.Server._FinalStand.Spawners;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._FinalStand.Structures;

public sealed partial class FSWaveDamageOnlySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWaveDamageOnlyComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, FSWaveDamageOnlyComponent comp, DamageModifyEvent args)
    {
        // Negative totals are welder repairs coming through the same path.
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        if (args.Origin is { } origin && HasComp<WaveSpawnedTagComponent>(origin))
            return;

        args.Damage = new DamageSpecifier();
    }
}
