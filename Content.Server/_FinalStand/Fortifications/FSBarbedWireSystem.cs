// Wave attackers that melee a wired barricade take a cut and a short slow. Nobody is credited for the cut.
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Fortifications;

public sealed partial class FSBarbedWireSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SlowingUpgradeSystem _slow = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const string SlowSource = "barbed-wire";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBarbedWireComponent, AttackedEvent>(OnAttacked);
    }

    private void OnAttacked(Entity<FSBarbedWireComponent> ent, ref AttackedEvent args)
    {
        if (!HasComp<WaveSpawnedTagComponent>(args.User))
            return;

        _damageable.TryChangeDamage(args.User, new DamageSpecifier(_proto.Index(Slash), ent.Comp.Damage));
        _slow.ApplySlow(args.User, SlowSource, ent.Comp.Slow, ent.Comp.SlowDuration);
    }
}
