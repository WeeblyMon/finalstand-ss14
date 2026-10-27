// Resolves the local player's health and stamina for the vitals block on the wave HUD.
using Content.Client._FinalStand.Ailments;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Ailments;
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
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSAilmentVisualsSystem _ailmentVisuals = default!;
    [Dependency] private FSHealthBarSystem _healthBars = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private readonly List<FSAilmentView> _views = new();

    private void UpdateVitals(WaveHudOverlay overlay)
    {
        overlay.HealthRatio = null;
        overlay.HealthInCrit = false;
        overlay.StaminaRatio = null;
        overlay.HealthCurrent = 0;
        overlay.HealthMax = 0;
        overlay.DownText = null;
        overlay.DownBadge = null;
        overlay.HealthTicks.Clear();
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
                    overlay.HealthTicks.Add(_ailments.Rules.RegenCap);
                    overlay.HealthTicks.Add(_healthBars.SoftCap);
                }
                else if (_thresholds.TryGetThresholdForState(player, MobState.Dead, out var dead, thresholds))
                {
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

        if (_mobState.IsCritical(player) && TryComp<FSAilmentStateComponent>(player, out var vitals))
        {
            if (vitals.Held)
                overlay.DownText = Loc.GetString("fs-casualty-state-held");
            else if (vitals.DownEnd is { } end)
                overlay.DownText = Loc.GetString("fs-casualty-state-clock", ("time", FSCasualtyBoardSystem.FormatClock(end - _timing.CurTime)));

            overlay.DownHeld = vitals.Held;
            if (_ailments.Has(player, FSAilmentSlot.Heart))
                overlay.DownBadge = _ailmentVisuals.Pip(FSAilmentSlot.Heart);
        }

        if (TryComp<StaminaComponent>(player, out var stamina) && stamina.CritThreshold > 0f)
            overlay.StaminaRatio = Math.Clamp(1f - stamina.StaminaDamage / stamina.CritThreshold, 0f, 1f);
    }

    private void UpdateStatusPills(EntityUid player, WaveHudOverlay overlay)
    {
        if (!TryComp<FSAilmentStateComponent>(player, out var vitals))
            return;

        _ailments.GetViews(player, _views);
        var now = _timing.CurTime;

        foreach (var slot in FSAilmentVisualsSystem.Slots)
        {
            if (!_ailments.Rules.Slots.TryGetValue(slot, out var info))
                continue;

            FSAilmentView? ailment = null;
            FSAilmentView? ward = null;
            foreach (var view in _views)
            {
                if (view.Slot != slot)
                    continue;

                if (view.Ward)
                    ward = view;
                else if (ailment is not { } current || Outlasts(view, current))
                    ailment = view;
            }

            var warning = vitals.Warnings.GetValueOrDefault(slot);
            if (ailment == null && ward == null && !info.Fixed)
                continue;

            var color = FSAilmentVisualsSystem.SlotColor(slot);
            if (ailment is { } shown)
            {
                overlay.StatusPills.Add(new WaveHudOverlay.VitalPill(
                    Timed(PillName(shown), shown.End, now),
                    color,
                    _ailmentVisuals.FixIcon(shown),
                    slot == FSAilmentSlot.Bleeding ? _ailments.GetBleedAmount(player) : 0f,
                    vitals.Mend.GetValueOrDefault(slot) / 10f,
                    warning,
                    WaveHudOverlay.VitalPillStyle.Active));
            }
            else if (ward is { } guard)
            {
                overlay.StatusPills.Add(new WaveHudOverlay.VitalPill(
                    Timed(Loc.GetString(guard.Name), guard.End, now), FSPalette.Ok, null, 0f, 0f, warning,
                    WaveHudOverlay.VitalPillStyle.Ward));
            }
            else
            {
                overlay.StatusPills.Add(new WaveHudOverlay.VitalPill(
                    Loc.GetString(info.Label), color, null, 0f, 0f, warning, WaveHudOverlay.VitalPillStyle.Empty));
            }
        }

        if (vitals.Regenerating)
            overlay.StatusPills.Add(new WaveHudOverlay.VitalPill(Loc.GetString("fs-vitals-regen"), FSPalette.Ok,
                null, 0f, 0f, 0, WaveHudOverlay.VitalPillStyle.Status));

        if (vitals.Recovering)
            overlay.StatusPills.Add(new WaveHudOverlay.VitalPill(Loc.GetString("fs-vitals-recovering"), FSPalette.Ok,
                null, 0f, 0f, 0, WaveHudOverlay.VitalPillStyle.Status));
    }

    private static bool Outlasts(FSAilmentView view, FSAilmentView other)
    {
        return other.End is { } otherEnd && (view.End is not { } end || end > otherEnd);
    }

    private string PillName(FSAilmentView shown)
    {
        var name = Loc.GetString(shown.Name);
        foreach (var view in _views)
        {
            if (view.Slot == shown.Slot && !view.Ward && view != shown && view.Suffix is { } suffix)
                name = Loc.GetString("fs-ailment-with-suffix", ("name", name), ("suffix", Loc.GetString(suffix)));
        }

        return name;
    }

    private static string Timed(string name, TimeSpan? end, TimeSpan now)
    {
        if (end is not { } until)
            return name;

        return $"{name} {FSAilmentVisualsSystem.Seconds(until - now)}s";
    }
}
