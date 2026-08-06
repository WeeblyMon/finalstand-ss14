// Lets FS shop upgrades stun targets that carry StunImmune (zombies). Vanilla stun sources stay blocked.
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class FSStunOverrideSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<TagPrototype> StunImmuneTag = "StunImmune";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<FSForceStunComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.ExpiresAt)
                continue;
            _stun.TryUnstun(uid);
            RemComp<FSForceStunComponent>(uid);
        }
    }

    public bool TryForceStun(EntityUid target, TimeSpan duration)
    {
        var hadImmunity = _tags.RemoveTag(target, StunImmuneTag);

        var stunApplied = _stun.TryUpdateStunDuration(target, duration);
        var kdApplied = _stun.TryKnockdown(target, duration, refresh: true, autoStand: true, drop: false, force: true);

        if (hadImmunity)
            _tags.AddTag(target, StunImmuneTag);


        if (stunApplied)
        {
            var expiry = _timing.CurTime + duration;
            var marker = EnsureComp<FSForceStunComponent>(target);
            if (expiry > marker.ExpiresAt)
                marker.ExpiresAt = expiry;
        }

        return stunApplied;
    }
}

[RegisterComponent]
public sealed partial class FSForceStunComponent : Component
{
    [DataField] public TimeSpan ExpiresAt = TimeSpan.Zero;
}
