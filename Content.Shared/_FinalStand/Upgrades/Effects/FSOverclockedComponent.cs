using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSOverclockedComponent : Component
{
    // upgrade level (1–3), set when the upgrade is purchased
    [DataField] public int Level = 1;

    // normalized spool 0..1 — networked so the client can drive the glow
    [AutoNetworkedField] public float Spool;

    // Ordnance research ramp multiplier - recomputed each refresh, not accumulated.
    public float ResearchRampMultiplier = 1f;

    // server-only timing, not networked
    public double LastShotTime;

    public const float SpoolGainPerShot  = 0.08f;  // ~12 shots to reach max spool
    public const float SpoolDecayDelay   = 0.6f;   // seconds after last shot before decaying
    public const float SpoolDecayRate    = 0.35f;  // per second
    public const float FireRateBonusPerLevel = 1.2f; // max fire-rate bonus per upgrade level at full spool
}
