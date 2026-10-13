using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Deployables;

namespace Content.Server._FinalStand.Deployables;

public sealed partial class FSNullFieldSystem : FSDeployableAuraSystem<FSNullFieldComponent>
{
    [Dependency] private SlowingUpgradeSystem _slow = default!;

    protected override float GetRadius(FSNullFieldComponent aura) => aura.Radius;

    protected override void ApplyTo(EntityUid target, EntityUid source, FSNullFieldComponent aura)
    {
        _slow.ApplySlow(target, "null-field", aura.SlowFactor, TimeSpan.FromSeconds(RefreshDuration));
    }
}
