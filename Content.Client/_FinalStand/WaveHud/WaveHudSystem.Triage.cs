// Feeds the triage panel from the casualty board's pushed list. Medical-only by construction: the
// server only sends that list to medical sessions, so a non-medic never has rows to draw.
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
        var ordered = new List<(FSCasualtyEntry Entry, float? Range)>(board.Entries.Count);
        foreach (var entry in board.Entries)
            ordered.Add((entry, board.DistanceTo(entry.Position)));

        ordered.Sort((a, b) =>
        {
            var state = Rank(b.Entry.State).CompareTo(Rank(a.Entry.State));
            if (state != 0)
                return state;

            return (a.Range ?? float.MaxValue).CompareTo(b.Range ?? float.MaxValue);
        });

        foreach (var (entry, range) in ordered)
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
                entry.Responder != null));
        }
    }

    private static int Rank(FSCasualtyState state) => state switch
    {
        FSCasualtyState.Dead => 2,
        FSCasualtyState.Critical => 1,
        _ => 0,
    };
}
