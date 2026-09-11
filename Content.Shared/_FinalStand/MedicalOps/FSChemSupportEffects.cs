using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
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
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    protected override void Effect(Entity<FSFriendlyFireComponent> entity, ref EntityEffectEvent<FSStaunchBleeding> args)
    {
        if (!HasComp<BloodstreamComponent>(entity))
            return;

        _bloodstream.TryModifyBleedAmount(entity.Owner, -args.Effect.Amount * args.Scale);
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

        // Piercing is its own component rather than a bonus category, so register a nameplate-only
        // buff as well - otherwise the recipient gets no HUD readout and no health bar outline.
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
