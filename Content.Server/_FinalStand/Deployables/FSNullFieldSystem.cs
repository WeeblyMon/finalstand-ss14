using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Deployables;

public sealed class FSNullFieldSystem : FSDeployableAuraSystem<FSNullFieldComponent>
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override float GetRadius(FSNullFieldComponent aura) => aura.Radius;

    protected override void ApplyTo(EntityUid target, EntityUid source, FSNullFieldComponent aura)
    {
        var slow = EnsureComp<FSSlowedComponent>(target);
        var needsRefresh = !MathHelper.CloseToPercent(slow.SlowFactor, aura.SlowFactor);

        slow.SlowFactor = aura.SlowFactor;
        slow.EndTime = _timing.CurTime + TimeSpan.FromSeconds(RefreshDuration);

        if (needsRefresh)
            _movement.RefreshMovementSpeedModifiers(target);
    }
}
