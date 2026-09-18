using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSArmorPiercingComponent : Component
{
    [DataField]
    public TimeSpan Until;
}

public sealed partial class FSStaunchBleedingSystem : EntityEffectSystem<FSFriendlyFireComponent, FSStaunchBleeding>
{
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private OrganLookupSystem _organs = default!;

    private readonly List<EntityUid> _woundables = new();

    protected override void Effect(Entity<FSFriendlyFireComponent> entity, ref EntityEffectEvent<FSStaunchBleeding> args)
    {
        if (!_organs.TryGetRootOrgan(entity.Owner, out var root))
            return;

        _woundables.Clear();
        _woundables.Add(root.Owner);

        foreach (var child in _wounds.GetAllWoundableChildren(root.Owner))
            _woundables.Add(child.Owner);

        foreach (var woundable in _woundables)
            _wounds.TryHaltAllBleeding(woundable);
    }
}

public sealed partial class FSStaunchBleeding : EntityEffectBase<FSStaunchBleeding>
{
    [DataField]
    public float Amount = 3f;
}

public sealed partial class FSApplyArmorPiercingSystem : EntityEffectSystem<FSFriendlyFireComponent, FSApplyArmorPiercing>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSMedicalBonusSystem _bonus = default!;

    protected override void Effect(Entity<FSFriendlyFireComponent> entity, ref EntityEffectEvent<FSApplyArmorPiercing> args)
    {
        var duration = TimeSpan.FromSeconds(args.Effect.Duration);
        var until = _timing.CurTime + duration;
        var comp = EnsureComp<FSArmorPiercingComponent>(entity);

        if (comp.Until < until)
            comp.Until = until;

        _bonus.ApplyBuff(entity.Owner,
            args.Effect.Source,
            EmptyBonuses,
            duration,
            args.Effect.Name is { } name ? Loc.GetString(name) : null);
    }

    private static readonly Dictionary<FSMedicalBonusCategory, float> EmptyBonuses = new();
}

public sealed partial class FSApplyArmorPiercing : EntityEffectBase<FSApplyArmorPiercing>
{
    [DataField]
    public float Duration = 45f;

    [DataField]
    public string Source = FSApplyCombatBuff.SourcePrefix + "etchant";

    [DataField]
    public LocId? Name;
}
