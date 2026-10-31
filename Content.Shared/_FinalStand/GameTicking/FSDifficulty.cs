using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.GameTicking;

[Serializable, NetSerializable]
public enum FSDifficulty : byte
{
    Normal,
    Hard,
    Nightmare,
}

public static class FSDifficultyRules
{
    public static float HealthMultiplier(FSDifficulty difficulty) => difficulty switch
    {
        FSDifficulty.Hard => 1.35f,
        FSDifficulty.Nightmare => 1.75f,
        _ => 1f,
    };

    public static float CountMultiplier(FSDifficulty difficulty) => difficulty switch
    {
        FSDifficulty.Hard => 1.25f,
        FSDifficulty.Nightmare => 1.5f,
        _ => 1f,
    };

    public static float DamageMultiplier(FSDifficulty difficulty) => difficulty switch
    {
        FSDifficulty.Hard => 1.2f,
        FSDifficulty.Nightmare => 1.4f,
        _ => 1f,
    };

    public static float RewardMultiplier(FSDifficulty difficulty) => difficulty switch
    {
        FSDifficulty.Hard => 1.25f,
        FSDifficulty.Nightmare => 1.6f,
        _ => 1f,
    };

    public static int VictoryBonusPerkPoints(FSDifficulty difficulty) => difficulty switch
    {
        FSDifficulty.Hard => 1,
        FSDifficulty.Nightmare => 2,
        _ => 0,
    };

    public static LocId NameLocId(FSDifficulty difficulty) => difficulty switch
    {
        FSDifficulty.Hard => "fs-difficulty-hard",
        FSDifficulty.Nightmare => "fs-difficulty-nightmare",
        _ => "fs-difficulty-normal",
    };
}
