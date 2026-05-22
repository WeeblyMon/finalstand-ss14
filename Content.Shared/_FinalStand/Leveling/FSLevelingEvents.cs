using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Leveling;

[Serializable, NetSerializable]
public sealed class FSLevelingUpdatedEvent : EntityEventArgs
{
    public int Level;
    public int Experience;
    public int XpToNextLevel;
    public int PrestigeLevel;
}

[Serializable, NetSerializable]
public sealed class FSPrestigeRequestMessage : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class FSBuyPrestigeBuffMessage : EntityEventArgs
{
    public string BuffId { get; init; } = "";
}

public sealed class FSLevelUpEvent : EntityEventArgs
{
    public EntityUid MindId;
    public int NewLevel;
    public int PrestigeLevel;
}
