// Resolves the local player's health and stamina for the vitals block on the wave HUD.
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;

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
        overlay.HealthCurrent = 0;
        overlay.HealthMax = 0;
        overlay.StatusPills.Clear();

        if (_player.LocalEntity is not { } player)
            return;

        UpdateStatusPills(player, overlay);

        if (TryComp<DamageableComponent>(player, out var damage)
            && TryComp<MobThresholdsComponent>(player, out var thresholds))
        {
            var total = _damageable.GetTotalDamage((player, damage));

            if (_thresholds.TryGetThresholdForState(player, MobState.Critical, out var crit, thresholds))
            {
                overlay.HealthMax = (int) crit.Value;
                overlay.HealthCurrent = Math.Max(0, (int) (crit.Value - total).Float());

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

    // Conditions that need a decision from the player, in the corner they already watch for health.
    private void UpdateStatusPills(EntityUid player, WaveHudOverlay overlay)
    {
        if (TryComp<BloodstreamComponent>(player, out var blood) && blood.BleedAmount > 0f)
            overlay.StatusPills.Add(("BLEEDING", true));

        // Bones sit on body parts nested a few containers deep, so the body is walked rather than
        // asked - this tree has no GetBodyChildren to call.
        var broken = 0;
        CountBrokenBones(player, ref broken, 0);
        if (broken > 0)
            overlay.StatusPills.Add((broken == 1 ? "BROKEN BONE" : $"{broken} BROKEN BONES", true));
    }

    private void CountBrokenBones(EntityUid root, ref int broken, int depth)
    {
        if (depth > 4 || !TryComp<ContainerManagerComponent>(root, out var mgr))
            return;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (TryComp<BoneComponent>(item, out var bone) && bone.BoneSeverity == BoneSeverity.Broken)
                    broken++;

                CountBrokenBones(item, ref broken, depth + 1);
            }
        }
    }
}
