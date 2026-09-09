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

    protected override void Effect(Entity<FSFriendlyFireComponent> entity, ref EntityEffectEvent<FSApplyArmorPiercing> args)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);
        var comp = EnsureComp<FSArmorPiercingComponent>(entity);

        if (comp.Until < until)
            comp.Until = until;
    }
}

public sealed partial class FSApplyArmorPiercing : EntityEffectBase<FSApplyArmorPiercing>
{
    [DataField]
    public float Duration = 45f;
}
