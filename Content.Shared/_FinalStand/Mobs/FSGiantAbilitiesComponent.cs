using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Mobs;

// three-move boss kit: an aerial gap-closer, a lane-clearing throw and a short punishing dash
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSGiantAbilitiesComponent : Component
{
    [DataField]
    public float GlobalCooldown = 3f;

    [DataField]
    public float SkyJumpCooldown = 18f;

    [DataField]
    public float BoulderCooldown = 11f;

    [DataField]
    public float DashCooldown = 9f;

    [DataField]
    public float SkyJumpMinRange = 7f;

    [DataField]
    public float SkyJumpMaxRange = 22f;

    [DataField]
    public float BoulderMinRange = 5f;

    [DataField]
    public float BoulderMaxRange = 20f;

    [DataField]
    public float DashMinRange = 2.5f;

    [DataField]
    public float DashMaxRange = 7f;

    [DataField]
    public float SkyJumpWindup = 0.5f;

    [DataField]
    public float SkyJumpAirTime = 1.4f;

    [DataField]
    public float SkyJumpRadius = 2.5f;

    [DataField]
    public float SkyJumpOuterRadius = 4.5f;

    [DataField]
    public float SkyJumpDamage = 55f;

    [DataField]
    public float SkyJumpKnockbackDistance = 6f;

    [DataField]
    public float SkyJumpKnockbackSpeed = 11f;

    [DataField]
    public float DashKnockbackDistance = 5.5f;

    [DataField]
    public float DashKnockbackSpeed = 9f;

    [DataField]
    public float ShakeRadius = 40f;

    [DataField]
    public float BoulderWindup = 0.75f;

    [DataField]
    public float BoulderSpeed = 13f;

    [DataField]
    public float DashWindup = 0.45f;

    [DataField]
    public float DashTravelTime = 0.5f;

    [DataField]
    public float DashDistance = 5f;

    [DataField]
    public float DashDamage = 45f;

    [DataField]
    public float DashHitRadius = 1.6f;

    [DataField]
    public EntProtoId LaunchEffect = "FSEffectGiantLaunchSmoke";

    [DataField]
    public EntProtoId ImpactEffect = "FSEffectGiantImpact";

    [DataField]
    public EntProtoId TelegraphProto = "FSGiantTelegraphRing";

    [DataField]
    public EntProtoId LaneProto = "FSGiantLaneMarker";

    [DataField]
    public EntProtoId FistProto = "FSGiantFist";

    [DataField]
    public EntProtoId BoulderProto = "FSGiantBoulder";

    [DataField]
    public SoundSpecifier? RoarSound = new SoundPathSpecifier("/Audio/Animals/space_dragon_roar.ogg");

    [DataField]
    public SoundSpecifier? ImpactSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");

    [AutoNetworkedField]
    public bool Airborne;

    public FSGiantAbility Current = FSGiantAbility.None;
    public TimeSpan PhaseEnd;
    public TimeSpan NextAbility;
    public TimeSpan NextSkyJump;
    public TimeSpan NextBoulder;
    public TimeSpan NextDash;
    public Vector2 LockedTarget;
    public Vector2 DashOrigin;
    public Vector2 DashLanding;
    public Vector2 DashHeading;
    public EntityUid? TelegraphEntity;
    public readonly List<EntityUid> LaneEntities = new();
}

[Serializable, NetSerializable]
public enum FSGiantAbility : byte
{
    None,
    SkyJumpWindup,
    SkyJumpAir,
    BoulderWindup,
    DashWindup,
    DashTravel,
}

[Serializable, NetSerializable]
public enum FSGiantAbilityVisuals : byte
{
    Airborne,
}
