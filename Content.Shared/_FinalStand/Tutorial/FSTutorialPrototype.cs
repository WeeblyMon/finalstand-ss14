// A paged role tutorial: shown the first time a player spawns as one of its jobs, reopenable from the escape menu.
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Tutorial;

[Prototype("fsTutorial")]
public sealed partial class FSTutorialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<JobPrototype>> Jobs = new();

    [DataField(required: true)]
    public List<FSTutorialPage> Pages = new();
}

[DataDefinition]
public sealed partial class FSTutorialPage
{
    [DataField(required: true)]
    public SpriteSpecifier Image = default!;

    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Text;
}

[Serializable, NetSerializable]
public sealed class FSTutorialShowEvent(string tutorial) : EntityEventArgs
{
    public readonly string Tutorial = tutorial;
}

[Serializable, NetSerializable]
public sealed class FSTutorialSeenEvent(string tutorial) : EntityEventArgs
{
    public readonly string Tutorial = tutorial;
}

[Serializable, NetSerializable]
public sealed class FSTutorialRequestEvent : EntityEventArgs;
