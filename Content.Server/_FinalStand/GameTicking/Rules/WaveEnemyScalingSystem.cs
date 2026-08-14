using System.Linq;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server._FinalStand.GameTicking.Rules;

// Scales a wave enemy's HP, speed, damage and fire rate by wave number (and, for damage, player count).
public sealed partial class WaveEnemyScalingSystem : EntitySystem
{
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;

    public void ScaleEnemyHp(EntityUid enemy, int wave)
    {
        var multiplier = GetHpMultiplier(wave);
        if (multiplier <= 1f || !TryComp<MobThresholdsComponent>(enemy, out var thresholds))
            return;

        var snapshot = new List<(FixedPoint2 damage, MobState state)>(thresholds.Thresholds.Select(kv => (kv.Key, kv.Value)));
        foreach (var (damage, state) in snapshot)
            _mobThresholds.SetMobStateThreshold(enemy, damage * multiplier, state, thresholds);
    }

    private static float GetHpMultiplier(int wave)
    {
        if (wave < 10) return 1f;
        if (wave < 20) return 2.8f;
        if (wave < 30) return 5.5f;
        return 9f;
    }

    public void ScaleEnemySpeed(EntityUid enemy, int wave)
    {
        if (wave <= 1) return;
        if (!TryComp<MovementSpeedModifierComponent>(enemy, out var move))
            return;
        const float MaxSpeedMultiplier = 2.5f;
        var multiplier = Math.Min(1f + (wave - 1) * 0.0096f, MaxSpeedMultiplier);
        _movementSpeed.ChangeBaseSpeed(enemy, move.BaseWalkSpeed * multiplier, move.BaseSprintSpeed * multiplier, move.Acceleration, move);
    }

    public void ScaleEnemyDamage(EntityUid enemy, int wave, int playerCount)
    {
        var multiplier = MathF.Min(1f + wave * (0.035f + (playerCount - 1) * 0.007f), 3.5f);
        if (multiplier <= 1f)
            return;

        if (TryComp<FSWaveDamageScaleComponent>(enemy, out var dmgScale))
            dmgScale.MeleeDamageMultiplier = multiplier;

        if (TryComp<FSFlamethrowerComponent>(enemy, out var flamer))
            flamer.ParticlesPerBurst = Math.Max(2, (int) MathF.Round(2f * multiplier));

        if (TryComp<FSTeslaZombieComponent>(enemy, out var tesla))
        {
            tesla.PrimaryDamageShock = 15f * multiplier;
            tesla.ChainDamageShock = 9f * multiplier;
        }
    }

    public void ScaleEnemyFireRate(EntityUid enemy, int wave)
    {
        var t = Math.Clamp((wave - 15f) / 5f, 0f, 1f);
        var rateMultiplier = 1f + t;
        if (rateMultiplier <= 1f)
            return;

        if (TryComp<FSFlamethrowerComponent>(enemy, out var flamer))
        {
            flamer.ParticleSpawnRate = 0.08f / rateMultiplier;
            flamer.AttackCooldown = 4f / rateMultiplier;
        }

        if (TryComp<FSTeslaZombieComponent>(enemy, out var tesla))
            tesla.AttackCooldown = 5f / rateMultiplier;
    }
}
