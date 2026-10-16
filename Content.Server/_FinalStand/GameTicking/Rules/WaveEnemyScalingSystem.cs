using System.Linq;
using Content.Shared._FinalStand.Armor;
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

    // The boss is tuned for this many players; below it the fight gets shorter and less frantic.
    private const int BossFullScalePlayers = 7;

    public void ScaleEnemyHp(EntityUid enemy, int wave, int playerCount, float difficulty = 1f)
    {
        var multiplier = GetHpMultiplier(wave) * difficulty;
        if (HasComp<FSGiantAbilitiesComponent>(enemy))
            multiplier *= GetBossHpBonus(wave) * GetBossPlayerHpFactor(playerCount);
        else
            multiplier *= GetPlayerHpFactor(playerCount);

        if (multiplier == 1f || !TryComp<MobThresholdsComponent>(enemy, out var thresholds))
            return;

        var snapshot = new List<(FixedPoint2 damage, MobState state)>(thresholds.Thresholds.Select(kv => (kv.Key, kv.Value)));
        foreach (var (damage, state) in snapshot)
            _mobThresholds.SetMobStateThreshold(enemy, damage * multiplier, state, thresholds);
    }

    private static readonly (int Wave, float Multiplier)[] HpCurve =
    [
        (1, 1f),
        (10, 2.8f),
        (20, 5.5f),
        (30, 9f),
    ];

    public static float GetHpMultiplier(int wave)
    {
        if (wave <= HpCurve[0].Wave)
            return HpCurve[0].Multiplier;

        for (var i = 1; i < HpCurve.Length; i++)
        {
            var (hiWave, hiMult) = HpCurve[i];
            if (wave > hiWave)
                continue;

            var (loWave, loMult) = HpCurve[i - 1];
            var t = (wave - loWave) / (float) (hiWave - loWave);
            return loMult + (hiMult - loMult) * t;
        }

        var last = HpCurve[^1];
        return last.Multiplier + (wave - last.Wave) * 0.35f;
    }

    // On top of the shared curve above, so the boss keeps outscaling the trash around it.
    private static float GetBossHpBonus(int wave)
        => MathF.Min(1f + wave * 0.06f, 3f);

    public const float HpPerExtraPlayer = 0.12f;
    public const float MaxPlayerHpFactor = 2.5f;

    public static float GetPlayerHpFactor(int playerCount)
        => MathF.Min(1f + HpPerExtraPlayer * (Math.Max(1, playerCount) - 1), MaxPlayerHpFactor);

    public static float GetBossPlayerHpFactor(int playerCount)
        => Math.Clamp(0.35f + 0.65f * Math.Max(1, playerCount) / BossFullScalePlayers, 0.45f, 1f);

    public static float GetBossCooldownFactor(int playerCount)
        => 1f + 0.15f * Math.Max(0, BossFullScalePlayers - Math.Max(1, playerCount));

    public void ScaleBossForPlayers(EntityUid enemy, int playerCount)
    {
        if (!TryComp<FSGiantAbilitiesComponent>(enemy, out var giant))
            return;

        giant.CooldownMultiplier = GetBossCooldownFactor(playerCount);

        if (TryComp<FSArmorComponent>(enemy, out var armor))
            armor.RegenRate *= Math.Min(1f, (float) Math.Max(1, playerCount) / BossFullScalePlayers);
    }

    public void ScaleEnemySpeed(EntityUid enemy, int wave, float modifier = 1f)
    {
        if (wave <= 1 && modifier == 1f) return;
        if (!TryComp<MovementSpeedModifierComponent>(enemy, out var move))
            return;
        const float MaxSpeedMultiplier = 2.5f;
        var multiplier = Math.Min(1f + (Math.Max(1, wave) - 1) * 0.0096f, MaxSpeedMultiplier) * modifier;
        _movementSpeed.ChangeBaseSpeed(enemy, move.BaseWalkSpeed * multiplier, move.BaseSprintSpeed * multiplier, move.Acceleration, move);
    }

    public void ScaleEnemyDamage(EntityUid enemy, int wave, int playerCount, float difficulty = 1f)
    {
        var scalingPlayers = Math.Max(1, playerCount);
        var multiplier = MathF.Min(1f + wave * (0.035f + (scalingPlayers - 1) * 0.007f), 3.5f) * difficulty;
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
