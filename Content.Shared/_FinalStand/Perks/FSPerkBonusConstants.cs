namespace Content.Shared._FinalStand.Perks;

// Every numeric perk formula, in one place. FSPerkDef generates its catalog text from these, so the
// shop can never advertise a rate the maths does not pay. Read by the buff hubs and the perk systems.
public static class FSPerkBonusConstants
{
    public const float StoppingPowerPerLevel = 0.04f;   // ranged, non-launcher only
    public const float GlassCannonPerLevel = 0.25f;     // outgoing, ranged (incl. launcher) and melee
    public const float BulletStormPerLevel = 0.08f;     // fire rate, any gun incl. launcher
    public const float SwordAndShieldPerLevel = 0.05f;  // outgoing melee damage
    public const float OfficerBuffPerLevel = 0.15f;     // ally buff, ranged (incl. launcher) and melee
    public const float DeathAuraPerStack = 0.02f;       // ranged and melee
    public const float PacifistPenalty = 0.25f;         // flat outgoing-damage penalty, ranged and melee
    public const float LegBreakerStaminaPerLevel = 25f; // stamina damage to the target on crit

    public const float JuggernaughtPerLevel = 0.15f;         // vs. wave-zombie-sourced damage only
    public const float SwordAndShieldResistPerLevel = 0.12f; // while wielding melee, not also a gun
    public const float GlassCannonIncomingMultiplier = 2.0f; // flat, any level >= 1
    public const float PacifistResistPerLevel = 0.20f;
    public const float RampageResistPerLevel = 0.03f;        // per stack, per level

    public const float LightweightPerLevel = 0.06f;
    public const float SpeedDemonPerLevel = 0.02f;   // per stack, per level
    public const float RampageSpeedPerLevel = 0.02f; // per stack, per level

    public const float RampageRegenPerLevel = 0.2f; // HP/s, per stack, per level

    public const float InvestorPerLevel = 0.005f;
    public const float MutualFundPerLevel = 0.0025f;
    public const float ProfiteerFraction = 0.014f;
    public const float ProfiteerHitBase = 30f;  // per ranged hit that raises enemy damage
    public const float ProfiteerKillBase = 200f; // per zombie kill

    public const float FieldMedicPerLevel = 0.15f;

    public const float DeathAuraStacksPerLevel = 5f;

    // Indexed by level - 1. These do not scale linearly, so they are tables rather than a rate.
    public static readonly float[] AdrenalineSeconds = [2.1f, 2.8f, 3.5f, 4.2f];
    public static readonly float[] LifeLeechHeal = [1f, 2f, 4f, 6f];
}
