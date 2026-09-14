// Resolves the local player's health and stamina for the vitals block on the wave HUD.
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private void UpdateVitals(WaveHudOverlay overlay)
    {
        overlay.HealthRatio = null;
        overlay.HealthInCrit = false;
        overlay.StaminaRatio = null;

        if (_player.LocalEntity is not { } player)
            return;

        if (TryComp<DamageableComponent>(player, out var damage)
            && TryComp<MobThresholdsComponent>(player, out var thresholds))
        {
            var total = _damageable.GetTotalDamage((player, damage));

            if (_thresholds.TryGetThresholdForState(player, MobState.Critical, out var crit, thresholds))
            {
                if (total < crit)
                {
                    overlay.HealthRatio = Math.Clamp(1f - ((FixedPoint2) (total / crit.Value)).Float(), 0f, 1f);
                }
                else if (_thresholds.TryGetThresholdForState(player, MobState.Dead, out var dead, thresholds))
                {
                    // Past crit the bar re-spans crit -> dead, so it keeps draining instead of pinning at empty.
                    var span = (dead.Value - crit.Value).Float();
                    var into = (total - crit.Value).Float();
                    overlay.HealthRatio = span <= 0f ? 0f : Math.Clamp(1f - into / span, 0f, 1f);
                    overlay.HealthInCrit = true;
                }
            }
            else if (_thresholds.TryGetThresholdForState(player, MobState.Dead, out var dead, thresholds))
            {
                overlay.HealthRatio = Math.Clamp(1f - ((FixedPoint2) (total / dead.Value)).Float(), 0f, 1f);
            }
        }

        // Stamina counts up to its crit threshold, so the bar is what is left before collapsing.
        if (TryComp<StaminaComponent>(player, out var stamina) && stamina.CritThreshold > 0f)
            overlay.StaminaRatio = Math.Clamp(1f - stamina.StaminaDamage / stamina.CritThreshold, 0f, 1f);
    }
}
