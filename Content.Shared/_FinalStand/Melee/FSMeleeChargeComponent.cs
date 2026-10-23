using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Melee;

// Heavy attacks wind up while the button is held and swing on release, harder and wider the longer they were held.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMeleeChargeComponent : Component
{
    [DataField]
    public float MaxChargeTime = 1.2f;

    // Damage scale from a tap to a full charge.
    [DataField]
    public float MinDamage = 0.6f;

    [DataField]
    public float MaxDamage = 2f;

    // Knockback, stagger, flinch and feel scale from a tap to a full charge.
    [DataField]
    public float MinForce = 0.4f;

    [DataField]
    public float MaxForce = 1.3f;

    [DataField]
    public SoundSpecifier? ChargeSound;

    // Played to the attacker once the charge is full.
    [DataField]
    public SoundSpecifier? FullSound;

    // Played where a fully charged swing connects.
    [DataField]
    public SoundSpecifier? FullHitSound;

    // Played where a fully charged slam lands, in time with its impact, hit or miss.
    [DataField]
    public SoundSpecifier? FullLandSound;

    [AutoNetworkedField]
    public TimeSpan? ChargeStart;

    // How charged the last heavy attack was, so its effects keep its size after the swing.
    [AutoNetworkedField]
    public float LastCharge;

    public EntityUid? ChargeStream;

    public bool WasFull => LastCharge >= 0.999f;

    public float ChargeAt(TimeSpan now)
    {
        if (ChargeStart is not { } start || MaxChargeTime <= 0f)
            return 0f;

        return Math.Clamp((float) (now - start).TotalSeconds / MaxChargeTime, 0f, 1f);
    }

    public FSMeleeJuiceProfile Apply(FSMeleeJuiceProfile profile)
    {
        var charged = profile.Charged(MathHelper.Lerp(MinDamage, MaxDamage, LastCharge), MathHelper.Lerp(MinForce, MaxForce, LastCharge));
        charged.FullCharge = WasFull;
        return charged;
    }
}
