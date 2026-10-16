using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Titles;

[Prototype("fsTitle")]
public sealed partial class FSTitlePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Description { get; private set; }

    [DataField]
    public Color Color { get; private set; } = Color.FromHex("#D4AF37");

    [DataField]
    public FSTitleBuff Buff { get; private set; } = new();

    [DataField]
    public LocId? BuffDescription { get; private set; }
}

[DataDefinition]
public sealed partial class FSTitleBuff
{
    public static readonly FSTitleBuff None = new();

    [DataField]
    public float Xp = 1f;

    [DataField]
    public float KillCredits = 1f;

    [DataField]
    public float ObjectiveCredits = 1f;

    [DataField]
    public float Damage = 1f;

    [DataField]
    public float BossDamage = 1f;

    [DataField]
    public float EndlessDamage = 1f;

    [DataField]
    public float DamageTaken = 1f;

    [DataField]
    public float Speed = 1f;

    [DataField]
    public float MaxHealth;

    [DataField]
    public float Healing = 1f;

    [DataField]
    public float DownedHealing = 1f;
}
