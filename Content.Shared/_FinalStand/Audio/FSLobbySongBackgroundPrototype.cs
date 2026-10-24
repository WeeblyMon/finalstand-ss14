using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Audio;

[Prototype("fsLobbySongBackground")]
public sealed partial class FSLobbySongBackgroundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ResPath Song { get; private set; }

    [DataField(required: true)]
    public ResPath Background { get; private set; }

    [DataField]
    public LocId Title { get; private set; } = "lobby-state-background-unknown-title";

    [DataField]
    public LocId Artist { get; private set; } = "lobby-state-background-unknown-artist";
}
