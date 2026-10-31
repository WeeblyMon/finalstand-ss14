namespace Content.Shared._FinalStand.Deployables;

// A sentry below this share of its magazine is low: its owner gets the alarm, which stays on until it's refilled.
public static class FSSentryAmmo
{
    public const float LowFraction = 0.2f;

    public static int LowThreshold(FSSentryTurretComponent turret)
    {
        return (int) MathF.Ceiling(turret.MaxAmmo * LowFraction);
    }
}
