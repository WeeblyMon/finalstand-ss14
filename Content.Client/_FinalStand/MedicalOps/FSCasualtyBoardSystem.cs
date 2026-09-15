using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSCasualtyBoardSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private FSCasualtyBoardWindow? _window;
    private List<FSCasualtyEntry> _entries = new();

    /// <summary>
    /// Live casualties, for the triage panel on the wave HUD. The server only pushes this to
    /// medical sessions, so a non-empty list already means the viewer is medical.
    /// </summary>
    public IReadOnlyList<FSCasualtyEntry> Entries => _entries;

    /// <summary>Range from the viewer to a casualty, or null when it cannot be resolved.</summary>
    public float? DistanceTo(NetCoordinates position)
    {
        Resolve(ViewerCoordinates(), position, out var distance, out _);
        return distance;
    }

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

        var origin = ViewerCoordinates();

        var rows = new List<FSCasualtyRow>(_entries.Count);
        foreach (var entry in _entries)
        {
            Resolve(origin, entry.Position, out var distance, out var direction);
            rows.Add(new FSCasualtyRow(entry, distance, direction));
        }

        _window.Populate(rows);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_window is not { IsOpen: true })
            return;

        var origin = ViewerCoordinates();

        foreach (var entry in _entries)
        {
            Resolve(origin, entry.Position, out var distance, out var direction);
            _window.UpdateBearing(entry.Patient, distance, direction);
        }
    }

    private MapCoordinates? ViewerCoordinates()
    {
        return _player.LocalEntity is { } ent && !TerminatingOrDeleted(ent)
            ? _xform.GetMapCoordinates(ent)
            : null;
    }

    private void Resolve(MapCoordinates? origin, NetCoordinates position, out float? distance, out Vector2? direction)
    {
        distance = null;
        direction = null;

        if (origin is not { } from)
            return;

        var coords = GetCoordinates(position);
        if (!coords.IsValid(EntityManager))
            return;

        var target = _xform.ToMapCoordinates(coords);
        if (target.MapId != from.MapId)
            return;

        distance = (target.Position - from.Position).Length();

        if (distance <= 0.1f)
            return;

        var screenDelta = _eye.WorldToScreen(target.Position) - _eye.WorldToScreen(from.Position);
        if (screenDelta.LengthSquared() > 0f)
            direction = Vector2.Normalize(screenDelta);
    }
}
