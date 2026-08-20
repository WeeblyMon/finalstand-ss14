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

// Lobby has no mind to read leveling off, so it asks and the server falls back to the database.
[Serializable, NetSerializable]
public sealed class FSLevelingRequestMessage : EntityEventArgs { }

public sealed class FSLevelUpEvent : EntityEventArgs
{
    public EntityUid MindId;
    public int NewLevel;
    public int PrestigeLevel;
}
