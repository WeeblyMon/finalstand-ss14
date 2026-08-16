using Robust.Shared.Audio;

namespace Content.Shared._FinalStand.Weapons;

// Pairs with BatterySelfRecharger (autoRechargePauseTime: 0) - applies a cooldown and plays a
// sound only when the battery is fully emptied. Partial shots recharge normally with no delay.
[RegisterComponent]
public sealed partial class FSWeaponRechargeAudioComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier RechargeStartSound = default!;

    [DataField]
    public TimeSpan EmptyPenaltyDuration = TimeSpan.FromSeconds(6);

    [DataField]
    public bool WasEmpty = false;
}
