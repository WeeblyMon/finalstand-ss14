using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Common.Barks;

[Prototype("bark")]
public sealed partial class BarkPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public SoundCollectionSpecifier? SoundCollection;

    [DataField]
    public HashSet<String>? SpeciesWhitelist;

    [DataField]
    public float MinPitch = 0.9f;

    [DataField]
    public float MaxPitch = 1.1f;

    [DataField]
    public float Volume = 1;

    [DataField]
    public float Frequency = 0.5f;

    [DataField]
    public bool Stop = false;

    [DataField]
    public bool Predictable = true;

    [DataField]
    public bool RoundStart { get; set; } = true;
}
