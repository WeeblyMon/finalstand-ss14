namespace Content.Server._FinalStand.Leveling;

// Shared with FSPlayerBonusSummarySystem so both read the exact same numbers.
internal static class FSPerkBonusConstants
{
    public const float StoppingPowerPerLevel = 0.04f;   // ranged, non-launcher only
    public const float GlassCannonPerLevel = 0.07f;     // ranged (incl. launcher) and melee
    public const float BulletStormPerLevel = 0.08f;     // fire rate, any gun incl. launcher
    public const float SwordAndShieldPerLevel = 0.05f;  // melee only
    public const float OfficerBuffPerLevel = 0.15f;     // ally buff, ranged (incl. launcher) and melee
    public const float DeathAuraPerStack = 0.02f;       // ranged and melee
    public const float PacifistPenalty = 0.25f;         // flat outgoing-damage penalty, ranged and melee
}
