// Feeds the triage panel from the casualty board's pushed list. Medical-only by construction: the
// server only sends that list to medical sessions, so a non-medic never has rows to draw.
using System.Numerics;
using Content.Client._FinalStand.MedicalOps;
using Content.Shared._FinalStand.MedicalOps;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    [Dependency] private FSCasualtyBoardSystem _board = default!;

    private const int MaxTriageRows = 5;

    private void UpdateTriage(WaveHudOverlay overlay)
    {
        overlay.TriageRows.Clear();

        var board = _board;
        if (board.Entries.Count == 0)
            return;

        // Dead first, then critical, then nearest - the order a medic would pick targets in.
        var ordered = new List<(FSCasualtyEntry Entry, float? Range, Vector2? Dir)>(board.Entries.Count);
        var self = _player.LocalEntity is { } local ? GetNetEntity(local) : (NetEntity?) null;

        foreach (var entry in board.Entries)
        {
            // Your own body is not a casualty you can be dispatched to, and it is the one row that
            // can never show a bearing.
            if (self is { } selfNet && entry.Patient == selfNet)
                continue;

            board.BearingTo(entry.Position, out var range, out var dir);
            ordered.Add((entry, range, dir));
        }

        ordered.Sort((a, b) =>
        {
            var state = Rank(b.Entry.State).CompareTo(Rank(a.Entry.State));
            if (state != 0)
                return state;

            return (a.Range ?? float.MaxValue).CompareTo(b.Range ?? float.MaxValue);
        });

        foreach (var (entry, range, dir) in ordered)
        {
            if (overlay.TriageRows.Count >= MaxTriageRows)
                break;

            var state = entry.State switch
            {
                FSCasualtyState.Dead => "DOWN",
                FSCasualtyState.Critical => "CRIT",
                _ => "HURT",
            };

            overlay.TriageRows.Add(new WaveHudOverlay.TriageRow(
                entry.Name,
                state,
                range is { } r ? $"{(int) r}m" : string.Empty,
                entry.State == FSCasualtyState.Critical,
                entry.Responder != null,
                dir));
        }
    }

    private static int Rank(FSCasualtyState state) => state switch
    {
        FSCasualtyState.Dead => 2,
        FSCasualtyState.Critical => 1,
        _ => 0,
    };
}
