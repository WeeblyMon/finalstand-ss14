using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Emplacement;

[RegisterComponent]
public sealed partial class FSEmplacementComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Kit;

    [DataField]
    public int Reserve = 1200;

    [DataField]
    public int FeedPerSecond = 25;

    [DataField]
    public float CoverMelee = 0.8f;

    [DataField]
    public TimeSpan PackTime = TimeSpan.FromSeconds(3);

    public List<EntityUid> Pieces = new();

    public EntityUid? OwnerMind;

    public bool Deployed;

    public bool Packed;
}

[RegisterComponent]
public sealed partial class FSEmplacementPieceComponent : Component
{
    public EntityUid Core;
}

[RegisterComponent]
public sealed partial class FSEmplacementCoverComponent : Component
{
    public float MeleeDamage = 1f;
}
