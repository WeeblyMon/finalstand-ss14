// Client half of the casualty board. Distance is viewer-relative, so it is resolved here.

using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSCasualtyBoardSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private FSCasualtyBoardWindow? _window;
    private List<FSCasualtyEntry> _entries = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FSCasualtyBoardEvent>(OnBoard);
    }

    private void OnBoard(FSCasualtyBoardEvent ev)
    {
        _entries = ev.Entries;

        if (ev.Open)
        {
            Toggle();
            return;
        }

        // A routine refresh only redraws a board the medic already has open.
        if (_window is { IsOpen: true })
            Refresh();
    }

    private void Toggle()
    {
        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        if (_window == null)
        {
            _window = new FSCasualtyBoardWindow();
            _window.OnRespond += patient => RaiseNetworkEvent(new FSRespondToCasualtyEvent(patient));
        }

        Refresh();
        _window.OpenCentered();
    }

    private void Refresh()
    {
        if (_window == null)
            return;

        var viewer = _player.LocalEntity;
        var origin = viewer is { } ent && !TerminatingOrDeleted(ent)
            ? _xform.GetMapCoordinates(ent)
            : (MapCoordinates?)null;

        var rows = new List<FSCasualtyRow>(_entries.Count);
        foreach (var entry in _entries)
        {
            float? distance = null;

            if (origin is { } from && GetCoordinates(entry.Position) is var coords && coords.IsValid(EntityManager))
            {
                var target = _xform.ToMapCoordinates(coords);
                if (target.MapId == from.MapId)
                    distance = (target.Position - from.Position).Length();
            }

            rows.Add(new FSCasualtyRow(entry, distance));
        }

        _window.Populate(rows);
    }
}
