// The Engineer's Hammer never damages a fortification, whatever the swing.
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Fortifications;

[Serializable, NetSerializable]
public sealed partial class FSHammerDoAfterEvent : SimpleDoAfterEvent;

public sealed class FSHammerSafetySystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSUpgradeableComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<FSUpgradeableComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is { } origin
            && _hands.TryGetActiveItem(origin, out var held)
            && HasComp<FSEngineerHammerComponent>(held))
            args.Damage *= 0;
    }
}
