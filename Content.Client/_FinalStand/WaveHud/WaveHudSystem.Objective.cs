using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.GameTicking;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    private FSWaveObjectiveStateEvent? _objective;
    private FSWaveObjectiveStateEvent? _objectiveDrawn;
    private int _objectiveSecondsDrawn = -1;

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
            _objectiveDrawn = null;
            return;
        }

        var seconds = objective.EndTime is { } end
            ? (int) Math.Ceiling(Math.Max(0, (end - _timing.CurTime).TotalSeconds))
            : -1;
        if (ReferenceEquals(objective, _objectiveDrawn) && seconds == _objectiveSecondsDrawn)
            return;

        _objectiveDrawn = objective;
        _objectiveSecondsDrawn = seconds;

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

        var progress = objective.Kind switch
        {
            FSWaveObjectiveKind.KillQuota
                or FSWaveObjectiveKind.HealQuota
                or FSWaveObjectiveKind.Bounty
                or FSWaveObjectiveKind.MeleeKills => $"{objective.Progress}/{objective.Target}",
            FSWaveObjectiveKind.NoDowns
                or FSWaveObjectiveKind.Fortress
                or FSWaveObjectiveKind.NoSpending => Loc.GetString("fs-hud-objective-holding"),
            FSWaveObjectiveKind.HoldTheLine => Loc.GetString("fs-hud-objective-ccc", ("lost", objective.Progress), ("limit", objective.Target)),
            FSWaveObjectiveKind.Headhunter => Loc.GetString("fs-hud-objective-hunting"),
            FSWaveObjectiveKind.SupplyRun => Loc.GetString("fs-hud-objective-distance", ("distance", objective.Progress)),
            FSWaveObjectiveKind.Untouchable => Loc.GetString("fs-hud-objective-unhurt", ("unhurt", objective.Progress), ("needed", objective.Target)),
            _ => null,
        };
        var timer = seconds >= 0 ? $"{seconds / 60}:{seconds % 60:D2}" : null;

        overlay.ObjectiveLabel = objective.Name.ToUpperInvariant();
        overlay.ObjectiveValue = progress != null && timer != null ? $"{progress}  {timer}" : progress ?? timer ?? string.Empty;
        overlay.ObjectiveColor = FSPalette.Warn;
    }
}
