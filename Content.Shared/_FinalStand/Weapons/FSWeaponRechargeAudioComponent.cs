using Robust.Shared.Audio;

namespace Content.Shared._FinalStand.Weapons;

/// <summary>
/// When added alongside BatterySelfRecharger (with autoRechargePauseTime: 0), applies a
/// configurable cooldown penalty and plays a sound only when the battery is fully emptied.
/// Partial shots recharge normally with no delay.
/// </summary>
[RegisterComponent]
public sealed partial class FSWeaponRechargeAudioComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier RechargeStartSound = default!;

    /// <summary>
    /// How long to block recharging after the battery is fully emptied.
    /// </summary>
    [DataField]
    public TimeSpan EmptyPenaltyDuration = TimeSpan.FromSeconds(6);

    [DataField]
    public bool WasEmpty = false;
}
