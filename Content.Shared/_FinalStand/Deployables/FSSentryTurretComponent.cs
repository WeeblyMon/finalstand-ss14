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

    // The gun art points south, which is also world angle zero, so no correction by default.
    [DataField]
    public float SpriteAngleOffset;

    public EntityUid? OwnerPlayer;

    [DataField]
    public float ProjectileSpeed = 25f;

    public TimeSpan NextFire;
}
