using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// deployed autoturret: fires at wave enemies in range until its magazine runs dry
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSentryTurretComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Ammo = 200;

    [DataField, AutoNetworkedField]
    public int MaxAmmo = 200;

    [DataField]
    public float Range = 3f;

    [DataField]
    public float FireInterval = 0.4f;

    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public EntProtoId ProjectileProto = "FSSentryBullet";

    [DataField]
    public float SpriteAngleOffset;

    [DataField]
    public float ProjectileSpeed = 25f;

    [DataField]
    public SoundSpecifier? FireSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/mk58.ogg");

    [DataField]
    public SoundSpecifier? TargetAcquiredSound;

    [DataField]
    public EntProtoId? CasingProto;

    public TimeSpan NextFire;

    public bool HasTarget;
}
