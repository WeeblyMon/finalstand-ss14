using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.GameTicking;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    private FSWaveObjectiveStateEvent? _objective;

    private void OnObjectiveState(FSWaveObjectiveStateEvent ev)
    {
        _objective = ev.Active ? ev : null;
        if (ev.Active && ev.Result == FSWaveObjectiveResult.Completed)
            FlashDirective(FSPalette.Money);
    }

    private void UpdateObjective(WaveHudOverlay overlay)
    {
        if (_objective is not { } objective)
        {
            overlay.ObjectiveLabel = null;
            return;
        }

        switch (objective.Result)
        {
            case FSWaveObjectiveResult.Completed:
                overlay.ObjectiveLabel = Loc.GetString("fs-hud-objective-done");
                overlay.ObjectiveValue = $"+${objective.Reward:N0}";
                overlay.ObjectiveColor = FSPalette.Money;
                return;
            case FSWaveObjectiveResult.Failed:
                overlay.ObjectiveLabel = Loc.GetString("fs-hud-objective-failed");
                overlay.ObjectiveValue = objective.Name;
                overlay.ObjectiveColor = FSPalette.TextDim;
                return;
        }

        var parts = new List<string>(2);
        if (objective.Kind == FSWaveObjectiveKind.KillQuota)
            parts.Add($"{objective.Progress}/{objective.Target}");
        else if (objective.Kind == FSWaveObjectiveKind.NoDowns)
            parts.Add(Loc.GetString("fs-hud-objective-holding"));

        if (objective.EndTime is { } end)
        {
            var secs = (int) Math.Ceiling(Math.Max(0, (end - _timing.CurTime).TotalSeconds));
            parts.Add($"{secs / 60}:{secs % 60:D2}");
        }

        overlay.ObjectiveLabel = objective.Name.ToUpperInvariant();
        overlay.ObjectiveValue = string.Join("  ", parts);
        overlay.ObjectiveColor = FSPalette.Warn;
    }
}
