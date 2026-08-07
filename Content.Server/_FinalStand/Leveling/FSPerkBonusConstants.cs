namespace Content.Server._FinalStand.Leveling;

// Every numeric perk formula, in one place, so a formula and the catalog text that describes it
// (Content.Shared/_FinalStand/Perks/FSPerkDef.cs) can never drift apart silently. Read by the buff
// hubs (FSPerkBuffSystem, FSIncomingDamagePerkSystem), the single-perk systems, and
// FSPlayerBonusSummarySystem, which all must show/apply the exact same number.
internal static class FSPerkBonusConstants
{
    // ── Outgoing damage and fire rate ──────────────────────────────────────
    public const float StoppingPowerPerLevel = 0.04f;   // ranged, non-launcher only
    public const float GlassCannonPerLevel = 0.07f;     // outgoing, ranged (incl. launcher) and melee
    public const float BulletStormPerLevel = 0.08f;     // fire rate, any gun incl. launcher
    public const float SwordAndShieldPerLevel = 0.05f;  // outgoing melee damage
    public const float OfficerBuffPerLevel = 0.15f;     // ally buff, ranged (incl. launcher) and melee
    public const float DeathAuraPerStack = 0.02f;       // ranged and melee
    public const float PacifistPenalty = 0.25f;         // flat outgoing-damage penalty, ranged and melee
    public const float LegBreakerStaminaPerLevel = 25f; // stamina damage to the target on crit

    // ── Incoming damage resistance (Content.Server/_FinalStand/Perks/FSIncomingDamagePerkSystem.cs) ──
    public const float JuggernaughtPerLevel = 0.15f;         // vs. wave-zombie-sourced damage only
    public const float SwordAndShieldResistPerLevel = 0.12f; // while wielding melee, not also a gun
    public const float GlassCannonIncomingMultiplier = 2.0f; // flat, any level >= 1
    public const float PacifistResistPerLevel = 0.20f;
    public const float RampageResistPerLevel = 0.03f;        // per stack, per level

    // ── Movement speed (RefreshMovementSpeedModifiersEvent handlers) ───────
    public const float LightweightPerLevel = 0.03f;
    public const float SpeedDemonPerLevel = 0.01f;   // per stack, per level
    public const float RampageSpeedPerLevel = 0.01f; // per stack, per level

    // ── Rampage regen (FSRampageSystem) ─────────────────────────────────────
    public const float RampageRegenPerLevel = 0.2f; // HP/s, per stack, per level

    // ── Economy ──────────────────────────────────────────────────────────
    public const float InvestorPerLevel = 0.025f;
    public const float MutualFundPerLevel = 0.0125f;
    public const float ProfiteerFraction = 0.07f;
    public const float ProfiteerHitBase = 30f;  // per ranged hit that raises enemy damage
    public const float ProfiteerKillBase = 200f; // per zombie kill

    // ── Field Medic (FSPlayerDamageModifySystem) ────────────────────────────
    public const float FieldMedicPerLevel = 0.15f;
}
