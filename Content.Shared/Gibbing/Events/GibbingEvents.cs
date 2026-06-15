namespace Content.Shared.Gibbing.Events;

public enum GibType : byte
{
    Gib,
    Drop,
    Delete,
    Skip,
}

public enum GibContentsOption : byte
{
    Skip,
    Drop,
    Gib,
}

[ByRefEvent]
public record struct AttemptEntityGibEvent(bool Cancelled);

[ByRefEvent]
public record struct AttemptEntityContentsGibEvent(EntityUid Contents, GibType GibType, GibContentsOption DropContents, bool Cancelled)
{
    /// <summary>
    ///     Goobstation: Container IDs to exclude from gibbing (e.g. wound/bone containers).
    /// </summary>
    public List<string>? ExcludedContainers;
}

[ByRefEvent]
public readonly record struct EntityGibbedEvent(HashSet<EntityUid> Giblets);
