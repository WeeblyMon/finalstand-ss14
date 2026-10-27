// Feeds the triage panel from the casualty board's pushed list.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    [Dependency] private FSCasualtyBoardSystem _board = default!;

    private const int MaxTriageRows = 5;

    private readonly List<(FSCasualtyEntry Entry, float? Range, Vector2? Dir)> _triageScratch = new();

    private void UpdateTriage(WaveHudOverlay overlay)
    {
        overlay.TriageRows.Clear();

        var board = _board;
        if (board.Entries.Count == 0)
            return;

        var ordered = _triageScratch;
        ordered.Clear();

        foreach (var entry in board.Entries)
        {
            board.BearingTo(entry.Position, out var range, out var dir);
            ordered.Add((entry, range, dir));
        }

        var now = _timing.CurTime;
        ordered.Sort((a, b) => FSCasualtyBoardSystem.Compare(a.Entry, a.Range, b.Entry, b.Range, now));

        foreach (var (entry, range, dir) in ordered)
        {
            if (overlay.TriageRows.Count >= MaxTriageRows)
                break;

            overlay.TriageRows.Add(new WaveHudOverlay.TriageRow(
                entry.Name,
                _board.StateLabel(entry, now),
                StateColor(entry),
                range is { } r ? $"{(int) r}m" : string.Empty,
                entry.Responder,
                dir,
                entry.Pips,
                _board.NeedsLabel(entry),
                entry.Priority));
        }
    }

    private static Color StateColor(FSCasualtyEntry entry) => entry.State switch
    {
        FSCasualtyState.Critical => entry.Held ? FSPalette.Ok : FSPalette.Warn,
        FSCasualtyState.Dead => FSPalette.Danger,
        _ => FSPalette.TextMuted,
    };
}
