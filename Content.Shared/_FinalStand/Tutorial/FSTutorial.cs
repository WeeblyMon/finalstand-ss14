using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Tutorial;

[Serializable, NetSerializable]
public enum FSTutorialTip : byte
{
    Welcome,
    Prep,
    FirstWave,
    Downed,
    Perks,
    DarkWave,
    BossWave,
}

public static class FSTutorialText
{
    public static string TitleLocId(FSTutorialTip tip) => $"fs-tutorial-{Key(tip)}-title";

    public static string BodyLocId(FSTutorialTip tip) => $"fs-tutorial-{Key(tip)}-body";

    public static string Key(FSTutorialTip tip) => tip switch
    {
        FSTutorialTip.Welcome => "welcome",
        FSTutorialTip.Prep => "prep",
        FSTutorialTip.FirstWave => "first-wave",
        FSTutorialTip.Downed => "downed",
        FSTutorialTip.Perks => "perks",
        FSTutorialTip.DarkWave => "dark-wave",
        FSTutorialTip.BossWave => "boss-wave",
        _ => "welcome",
    };
}

[Serializable, NetSerializable]
public sealed class FSTutorialTipEvent : EntityEventArgs
{
    public FSTutorialTip Tip;
}
