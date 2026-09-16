using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[Serializable, NetSerializable]
public enum FSSyringeFillerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSSyringeFillerBuiState : BoundUserInterfaceState
{
    public string? SourceName;
    public string? SourceContents;
    public float SourceVolume;
    public float SourceMax;

    public string? MagazineName;
    public string? MagazineContents;
    public float MagazineVolume;
    public float MagazineMax;
    public int Darts;
    public int MaxDarts;

    public string Status = string.Empty;
    public bool Running;

    // Sent as a window rather than a percentage so the bar interpolates locally instead of needing
    // a state push per tick.
    public TimeSpan StartedAt;
    public TimeSpan FinishAt;
    public bool CanFill;
    public bool CanPurge;
}

[Serializable, NetSerializable]
public sealed class FSSyringeFillerFillMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSSyringeFillerPurgeMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSSyringeFillerEjectMessage : BoundUserInterfaceMessage
{
    public bool Source;

    public FSSyringeFillerEjectMessage(bool source)
    {
        Source = source;
    }
}
