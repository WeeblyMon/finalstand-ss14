using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSRevenantComponent : Component
{
    [DataField] public float TargetSearchRange = 100f;
    [DataField] public float MarkSearchRange = 100f;
    [DataField] public float MarkScanCooldown = 30f;

    [DataField] public float MarkRescanDelay = 4f;
    [DataField] public float MarkIsolationRadius = 6f;

    [DataField] public float MarkDuration = 35f;

    [DataField] public float MarkBacklineWeight = 0.6f;
    [DataField] public float MarkIsolationWeight = 0.4f;

    [DataField] public float GlobalCooldown = 0.6f;

    [DataField] public float StalkRange = 6f;
    [DataField] public float EngageRange = 8f;
    [DataField] public float RetreatDuration = 5f;
    [DataField] public float RetreatMinDuration = 1.5f;
    [DataField] public float RetreatRange = 12f;
    [DataField] public float ComboStepTimeout = 2.5f;

    [DataField] public float MarkGraceDuration = 8f;

    [DataField] public float DarkWaveRetreatDuration = 2.5f;
    [DataField] public float DarkWaveRetreatMinDuration = 0.8f;
    [DataField] public float DarkWaveRetreatRange = 7f;

    [DataField] public float ResistanceBypass = 0.10f;

    [DataField] public float GrabCooldown = 8f;
    [DataField] public float GrabRange = 6f;
    [DataField] public float GrabMinRange = 2.5f;
    [DataField] public float GrabLandDistance = 1f;
    [DataField] public float GrabPullSpeed = 16f;
    [DataField] public float GrabDamage = 6.3f;
    [DataField] public float GrabPauseDuration = 0.6f;

    [DataField] public float BindCooldown = 10f;
    [DataField] public float BindDuration = 1.2f;
    [DataField] public float BindDamage = 5.25f;

    [DataField] public float SliceCooldown = 1.5f;
    [DataField] public float MeleeRange = 1.5f;
    [DataField] public float SweepRange = 1.8f;
    [DataField] public float SweepArcDegrees = 90f;
    [DataField] public float SliceHitDelay = 0.25f;
    [DataField] public float SliceDamage = 28.35f;

    [DataField] public float ExecuteHealthThreshold = 0.30f;
    [DataField] public float ExecuteWindupDuration = 0.4f;
    [DataField] public float ExecuteDamage = 1050f;
    [DataField] public float ExecuteEscapeTolerance = 3f;

    [DataField] public float BoltCooldown = 5f;
    [DataField] public float BoltMaxRange = 12f;
    [DataField] public int BoltCount = 4;
    [DataField] public float BoltSpreadDegrees = 25f;
    [DataField] public float BoltSpeed = 12f;
    [DataField] public float BoltDamage = 15.15f;

    public float GcdAccum;
    public float MarkAccum;
    public float GrabAccum;
    public float BindAccum;
    public float SliceAccum;
    public float BoltAccum;
    public float GrabPauseAccum;
    public float ExecuteWindupAccum;
    public bool UseVerticalNext = true;
    public EntityUid? MarkedTarget;
    public EntityUid? ExecuteTarget;
    public EntityUid? GrabTarget;
    public bool IsExecuting;
    public bool IsGrabPaused;
    public bool DeathAnnounced;
    public EntityUid? CurrentTarget;

    public bool OrbitClockwise;
    public TimeSpan MarkedAt;
    public int LightStunCount;
    public TimeSpan LightStunWindowEnd;
    public TimeSpan LightStunImmuneUntil;

    public FSRevenantPhase Phase = FSRevenantPhase.Stalk;
    public float PhaseAccum;

    public FSRevenantAbility? LockedAbility;
    public bool? LastLockedOutcome;
}

public enum FSRevenantPhase : byte
{
    Stalk,
    Grab,
    Bind,
    Volley,
    SliceOne,
    SliceTwo,
    Execute,
    Retreat,
}

public enum FSRevenantAbility : byte
{
    Execute,
    Grab,
    Bind,
    Slice,
    Bolt,
}
