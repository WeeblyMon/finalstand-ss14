using System.Numerics;
using Content.Client._FinalStand.Ailments;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed partial class FSCasualtyBoardSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const FSAilmentPips MajorNeeds = FSAilmentPips.Bleeding | FSAilmentPips.Bone;

    private FSCasualtyBoardWindow? _window;
    private List<FSCasualtyEntry> _entries = new();

    public IReadOnlyList<FSCasualtyEntry> Entries => _entries;

    public void BearingTo(NetCoordinates position, out float? distance, out Vector2? direction)
    {
        Resolve(ViewerCoordinates(), position, out distance, out direction);
    }

    public static int Compare(FSCasualtyEntry a, float? rangeA, FSCasualtyEntry b, float? rangeB, TimeSpan now)
    {
        var priority = b.Priority.CompareTo(a.Priority);
        if (priority != 0)
            return priority;

        var rank = Rank(a).CompareTo(Rank(b));
        if (rank != 0)
            return rank;

        if (a.State == FSCasualtyState.Critical)
        {
            var clock = SecondsLeft(a, now).CompareTo(SecondsLeft(b, now));
            if (clock != 0)
                return clock;
        }

        return (rangeA ?? float.MaxValue).CompareTo(rangeB ?? float.MaxValue);
    }

    private static int Rank(FSCasualtyEntry entry) => entry.State switch
    {
        FSCasualtyState.Critical => 0,
        FSCasualtyState.Dead => 1,
        _ => (entry.Needs & MajorNeeds) != 0 ? 2 : 3,
    };

    private static double SecondsLeft(FSCasualtyEntry entry, TimeSpan now)
    {
        return entry.Held || entry.DownEnd is not { } end ? double.MaxValue : (end - now).TotalSeconds;
    }

    public string StateLabel(FSCasualtyEntry entry, TimeSpan now)
    {
        return entry.State switch
        {
            FSCasualtyState.Dead => Loc.GetString("fs-casualty-state-dead"),
            FSCasualtyState.Critical when entry.Held => Loc.GetString("fs-casualty-state-held"),
            FSCasualtyState.Critical when entry.DownEnd is { } end =>
                Loc.GetString("fs-casualty-state-clock", ("time", FormatClock(end - now))),
            FSCasualtyState.Critical => Loc.GetString("fs-casualty-state-critical"),
            _ => Loc.GetString("fs-casualty-state-hurt"),
        };
    }

    public static Color StateColor(FSCasualtyEntry entry) => entry.State switch
    {
        FSCasualtyState.Critical => entry.Held ? FSPalette.Ok : FSPalette.Danger,
        FSCasualtyState.Dead => FSPalette.TextDim,
        _ => FSPalette.Warn,
    };

    public static string FormatClock(TimeSpan left)
    {
        var seconds = FSAilmentVisualsSystem.Seconds(left);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    public string NeedsLabel(FSCasualtyEntry entry)
    {
        var parts = new List<string>();
        if (entry.NeedsDefib)
            parts.Add(Loc.GetString("fs-need-defib"));

        foreach (var slot in FSAilmentVisualsSystem.Slots)
        {
            if (FSAilmentVisualsSystem.Has(entry.Needs, slot)
                && _ailments.Rules.Slots.TryGetValue(slot, out var info)
                && info.Need is { } need)
            {
                parts.Add(Loc.GetString(need));
            }
        }

        return string.Join(" ", parts);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FSCasualtyBoardEvent>(OnBoard);
        SubscribeNetworkEvent<FSMedicalStatusEvent>(OnMedicalStatus);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Clear());
    }

    private void OnMedicalStatus(FSMedicalStatusEvent ev)
    {
        if (!ev.IsMedical)
            Clear();
    }

    private void Clear()
    {
        _entries = new();
        _window?.Close();
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
            _window.OnTag += patient => RaiseNetworkEvent(new FSPriorityTagRequestEvent(patient));
        }

        Refresh();
        _window.OpenCentered();
    }

    private void Refresh()
    {
        if (_window == null)
            return;

        var origin = ViewerCoordinates();
        var now = _timing.CurTime;

        var rows = new List<FSCasualtyRow>(_entries.Count);
        foreach (var entry in _entries)
        {
            Resolve(origin, entry.Position, out var distance, out var direction);
            rows.Add(new FSCasualtyRow(entry, distance, direction, StateLabel(entry, now), NeedsLabel(entry)));
        }

        rows.Sort((a, b) => Compare(a.Entry, a.Distance, b.Entry, b.Distance, now));
        _window.Populate(rows, HasComp<FSCmoPanelComponent>(_player.LocalEntity));
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
