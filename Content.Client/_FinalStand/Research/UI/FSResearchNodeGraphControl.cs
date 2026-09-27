using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Research.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Research.UI;

public enum FSResearchGraphStyle
{
    Blueprint,
    Schematic,
}

public sealed record FSResearchLine(string Key, string Name, int Done, int Total);

// Research tree canvas. Layout is in design pixels; K converts them to screen pixels for the current zoom.
public sealed partial class FSResearchNodeGraphControl : Control
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const float MinZoom = 0.5f;
    public const float MaxZoom = 2f;
    private const float DragThreshold = 4f;
    private const string StrayLineKey = "__strays";

    private readonly FSResearchClientSystem _fsResearch;
    private readonly FSSpriteCrop _icons = new();
    private readonly FontResource _boldFont;
    private readonly FontResource _regularFont;
    private readonly FontResource _monoFont;
    private readonly Texture _discTexture;
    private readonly Texture _glowTexture;
    private readonly Dictionary<(FontResource, int), Font> _fonts = new();
    private readonly Dictionary<string, List<string>> _wrapCache = new();
    private Font? _wrapFont;

    private EntityUid? _console;
    private FSResearchGraphStyle _style = FSResearchGraphStyle.Blueprint;
    private string _filter = "";
    private string? _branchFilter;
    private string? _lineFilter;
    private float _zoom = 1f;
    private bool _layoutStale = true;
    private bool _medical;

    private ScrollContainer? _scroll;
    private bool _leftDown;
    private bool _dragging;
    private Vector2 _dragStartCursor;
    private Vector2 _dragStartScroll;
    private Func<Vector2>? _pendingScroll;
    private int _pendingScrollFrames;

    private readonly List<FSResearchNodeView> _nodes = new();
    private readonly Dictionary<string, FSResearchNodeView> _nodeById = new();
    private readonly Dictionary<string, FSResearchNodeView> _branchById = new();
    private readonly Dictionary<string, Vector2> _positions = new();
    private readonly Dictionary<string, List<FSResearchNodeView>> _childrenByParent = new();
    private readonly List<List<FSResearchNodeView>> _exclusiveGroups = new();
    private readonly List<(int Tier, float Y)> _tierRows = new();
    private readonly Dictionary<string, string> _refs = new();
    private readonly List<(string Key, string Name, List<string> Ids)> _lines = new();
    private Vector2 _contentSize;

    public event Action<FSResearchNodeView>? OnNodeSelected;
    public event Action? ZoomChanged;

    public string? SelectedId { get; set; }
    public string? HoveredId { get; private set; }

    public FSResearchNodeGraphControl()
    {
        IoCManager.InjectDependencies(this);
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;

        _fsResearch = _entityManager.System<FSResearchClientSystem>();
        _boldFont = _resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf");
        _regularFont = _resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf");
        _monoFont = _resourceCache.GetResource<FontResource>("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf");

        const string dir = "/Textures/_FinalStand/Interface/Research/";
        _discTexture = _resourceCache.GetResource<TextureResource>(dir + "node_disc.png").Texture;
        _glowTexture = _resourceCache.GetResource<TextureResource>(dir + "node_glow.png").Texture;
    }

    public FSResearchGraphStyle Style
    {
        get => _style;
        set
        {
            if (_style == value)
                return;
            _style = value;
            _layoutStale = true;
            Rebuild();
            FocusFirstNode();
        }
    }

    public float Zoom => _zoom;
    public string? LineFilter => _lineFilter;

    public IEnumerable<FSResearchLine> Lines => _lines.Select(l => new FSResearchLine(
        l.Key, l.Name, l.Ids.Count(id => _branchById.TryGetValue(id, out var n) && n.IsDone), l.Ids.Count));

    private float K => UIScale * _zoom;
    private bool Blueprint => _style == FSResearchGraphStyle.Blueprint;

    public void SetScrollContainer(ScrollContainer scroll) => _scroll = scroll;

    public void SetConsole(EntityUid console)
    {
        if (_console != console)
        {
            _console = console;
            _zoom = 1f;
            _layoutStale = true;
        }

        if (_layoutStale)
        {
            Rebuild();
            FocusFirstNode();
        }
        else
        {
            RefreshNodeStates();
        }
    }

    public void InvalidateLayout() => _layoutStale = true;

    public FSResearchNodeView? GetNode(string id) => _branchById.GetValueOrDefault(id);

    public string? RefOf(string id) => _refs.GetValueOrDefault(id);

    public void SetFilter(string filter) => _filter = filter.Trim();

    public void SetBranchFilter(string? branch)
    {
        if (_branchFilter == branch && !_layoutStale)
            return;
        _branchFilter = branch;
        _lineFilter = null;
        Rebuild();
        FocusFirstNode();
    }

    public void SetLineFilter(string? lineKey)
    {
        if (_lineFilter == lineKey)
            return;
        _lineFilter = lineKey;
        Rebuild();
        FocusFirstNode();
    }

    public void ResetView()
    {
        SetZoom(1f);
        FocusFirstNode();
    }

    // Zooms around the middle of the viewport.
    public void ZoomBy(float factor)
    {
        if (_scroll == null)
        {
            SetZoom(_zoom * factor);
            return;
        }

        ZoomAround(_scroll.Size * UIScale / 2f + ScrollPixels(), _zoom * factor);
    }

    private void SetZoom(float zoom)
    {
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (MathHelper.CloseTo(zoom, _zoom))
            return;
        _zoom = zoom;
        UpdateMinSize();
        ZoomChanged?.Invoke();
    }

    // Keeps the design point under `local` fixed on screen while the canvas rescales.
    private void ZoomAround(Vector2 local, float zoom)
    {
        var anchor = (local - Origin()) / K;
        var inViewport = local - ScrollPixels();
        SetZoom(zoom);
        QueueScroll(() => (anchor * K + Origin() - inViewport) / UIScale);
    }

    private void FocusFirstNode()
    {
        if (_positions.Count == 0)
            return;

        var first = _positions.Values.OrderBy(p => p.Y).ThenBy(p => p.X).First();
        QueueScroll(() =>
        {
            var viewport = _scroll?.Size ?? Size;
            var at = (first * K + Origin()) / UIScale;
            var x = at.X < viewport.X * 0.75f ? 0f : at.X - viewport.X / 2f;
            return new Vector2(x, at.Y - Metrics.Above * _zoom - 24f);
        });
    }

    // Scroll limits only update after the next layout pass, so the target is applied a couple of frames later.
    private void QueueScroll(Func<Vector2> target)
    {
        _pendingScroll = target;
        _pendingScrollFrames = 2;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_pendingScroll == null || --_pendingScrollFrames > 0)
            return;

        var target = _pendingScroll();
        _pendingScroll = null;
        if (_scroll == null)
            return;
        _scroll.HScroll = Math.Max(0f, target.X);
        _scroll.VScroll = Math.Max(0f, target.Y);
    }

    private Vector2 ScrollPixels()
        => _scroll == null ? Vector2.Zero : new Vector2(_scroll.HScroll, _scroll.VScroll) * UIScale;

    // Narrow trees sit centred in the viewport instead of hugging the left edge.
    private Vector2 Origin()
        => new(MathF.Max(0f, (PixelWidth - _contentSize.X * K) / 2f), 0f);

    private void UpdateMinSize()
    {
        MinWidth = _contentSize.X * _zoom;
        MinHeight = _contentSize.Y * _zoom;
    }

    private string CostText(int cost)
        => string.Format(CultureInfo.InvariantCulture, _medical ? "${0:N0}" : "{0:N0} RP", cost);

    private Font FontOf(FontResource resource, float designPx)
    {
        var size = Math.Max(6, (int) MathF.Round(designPx * K * 0.75f));
        if (!_fonts.TryGetValue((resource, size), out var font))
            _fonts[(resource, size)] = font = new VectorFont(resource, size);
        return font;
    }

    private void Rebuild()
    {
        _nodes.Clear();
        _nodeById.Clear();
        _branchById.Clear();
        _positions.Clear();
        _childrenByParent.Clear();
        _exclusiveGroups.Clear();
        _tierRows.Clear();
        _refs.Clear();
        _lines.Clear();
        _wrapCache.Clear();
        _contentSize = Vector2.Zero;
        _layoutStale = false;

        if (_console is not { } console || !_entityManager.HasComponent<TechnologyDatabaseComponent>(console))
        {
            UpdateMinSize();
            return;
        }

        _entityManager.TryGetComponent<FSTechDatabaseComponent>(console, out var fsDb);
        _medical = fsDb?.Track == FSResearchTrack.Medical;
        var allowed = fsDb?.Branches is { Count: > 0 } b ? b : null;
        var branchOrder = _prototype.EnumeratePrototypes<FSTechBranchPrototype>()
            .Where(x => allowed == null || allowed.Contains(x.ID))
            .OrderBy(x => x.SortOrder)
            .Select(x => x.ID)
            .ToList();

        var branchNodes = _prototype.EnumeratePrototypes<FSTechNodePrototype>()
            .Where(p => !p.Hidden && (allowed == null || allowed.Contains(p.Branch)) && (_branchFilter == null || p.Branch == _branchFilter))
            .Select(p => new FSResearchNodeView(p))
            .ToList();

        foreach (var node in branchNodes)
            _branchById[node.Id] = node;

        BuildLines(branchNodes, branchOrder);

        var shown = branchNodes;
        if (_lineFilter != null)
        {
            var line = _lines.FirstOrDefault(l => l.Key == _lineFilter);
            if (line.Ids != null)
            {
                var keep = line.Ids.ToHashSet();
                shown = branchNodes.Where(n => keep.Contains(n.Id)).ToList();
            }
            else
            {
                _lineFilter = null;
            }
        }

        _nodes.AddRange(shown);
        foreach (var node in _nodes)
            _nodeById[node.Id] = node;

        LayOut(branchOrder);
        BuildDrawIndexes();
        RefreshNodeStates();
        UpdateMinSize();
    }

    // Columns are assigned in whole units first, then spaced out by the active style's metrics.
    private void LayOut(List<string> branchOrder)
    {
        var m = Metrics;
        var connectedIds = new HashSet<string>();
        foreach (var node in _nodes)
        {
            foreach (var prereq in node.AllPrerequisiteIds)
            {
                if (!_nodeById.ContainsKey(prereq))
                    continue;
                connectedIds.Add(node.Id);
                connectedIds.Add(prereq);
            }
        }

        var connected = _nodes.Where(n => connectedIds.Contains(n.Id)).ToList();
        var strays = _nodes.Where(n => !connectedIds.Contains(n.Id))
            .OrderBy(n => branchOrder.IndexOf(n.GroupId))
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        var tiers = connected.Select(n => n.Tier).Distinct().OrderBy(t => t).ToList();
        if (tiers.Count == 0)
            tiers.Add(strays.Count > 0 ? strays.Min(n => n.Tier) : 1);
        var rowOfTier = new Dictionary<int, int>();
        for (var i = 0; i < tiers.Count; i++)
            rowOfTier[tiers[i]] = i;

        var column = new Dictionary<string, float>();
        var cursor = 0f;
        foreach (var branch in branchOrder)
        {
            var inBranch = connected.Where(n => n.GroupId == branch).ToList();
            foreach (var component in GroupIntoComponents(inBranch)
                         .OrderBy(c => c.Min(n => n.Tier))
                         .ThenBy(c => c.Select(n => n.Id).Min(StringComparer.Ordinal)))
            {
                var placed = PlaceComponent(component, cursor);
                foreach (var (id, x) in placed)
                    column[id] = x;
                cursor = placed.Values.Max() + 1.5f;
            }
        }

        for (var i = 0; i < strays.Count; i++)
            column[strays[i].Id] = cursor + i / tiers.Count;

        var rowOf = new Dictionary<string, int>();
        foreach (var node in connected)
            rowOf[node.Id] = rowOfTier[node.Tier];
        for (var i = 0; i < strays.Count; i++)
            rowOf[strays[i].Id] = i % tiers.Count;

        var left = m.Gutter + m.Col / 2f;
        var top = m.Above + 16f;
        foreach (var node in _nodes)
            _positions[node.Id] = new Vector2(left + column[node.Id] * m.Col, top + rowOf[node.Id] * m.Row);
        for (var i = 0; i < tiers.Count; i++)
            _tierRows.Add((tiers[i], top + i * m.Row));

        var maxColumn = column.Count > 0 ? column.Values.Max() : 0f;
        _contentSize = new Vector2(left + maxColumn * m.Col + m.Col / 2f + 24f, top + (tiers.Count - 1) * m.Row + m.Below + 24f);

        var index = 1;
        foreach (var (id, _) in _positions.OrderBy(kv => kv.Value.Y).ThenBy(kv => kv.Value.X))
            _refs[id] = "U" + index++;
    }

    // Parents sit over the average of their children; siblings keep at least one column apart.
    private static Dictionary<string, float> PlaceComponent(List<FSResearchNodeView> component, float start)
    {
        var x = new Dictionary<string, float>();
        var nextFree = start;
        var tiers = component.GroupBy(n => n.Tier).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();

        foreach (var tier in tiers)
        {
            var ideal = new Dictionary<string, float>();
            foreach (var node in tier)
            {
                var parents = node.AllPrerequisiteIds.Where(x.ContainsKey).Select(id => x[id]).ToList();
                if (parents.Count > 0)
                    ideal[node.Id] = parents.Average();
            }

            foreach (var root in tier.Where(n => !ideal.ContainsKey(n.Id)).OrderBy(n => n.Id, StringComparer.Ordinal))
                ideal[root.Id] = nextFree++;

            Spread(tier.OrderBy(n => ideal[n.Id]).ThenBy(n => n.Id, StringComparer.Ordinal), ideal, x);
        }

        var children = new Dictionary<string, List<string>>();
        foreach (var node in component)
        {
            foreach (var parent in node.AllPrerequisiteIds.Where(x.ContainsKey))
            {
                if (!children.TryGetValue(parent, out var list))
                    children[parent] = list = new List<string>();
                list.Add(node.Id);
            }
        }

        for (var t = tiers.Count - 1; t >= 0; t--)
        {
            var ideal = new Dictionary<string, float>();
            foreach (var node in tiers[t])
                ideal[node.Id] = children.TryGetValue(node.Id, out var kids) ? kids.Average(k => x[k]) : x[node.Id];
            Spread(tiers[t].OrderBy(n => x[n.Id]), ideal, x);
        }

        var shift = start - x.Values.Min();
        foreach (var id in x.Keys.ToList())
            x[id] += shift;
        return x;
    }

    private static void Spread(IEnumerable<FSResearchNodeView> ordered, Dictionary<string, float> ideal, Dictionary<string, float> x)
    {
        float? previous = null;
        foreach (var node in ordered)
        {
            var value = ideal[node.Id];
            if (previous is { } p && value < p + 1f)
                value = p + 1f;
            x[node.Id] = value;
            previous = value;
        }
    }

    // Each connected sub-tree becomes a line, named after its shop unlock or its deepest node.
    private void BuildLines(List<FSResearchNodeView> nodes, List<string> branchOrder)
    {
        var ids = nodes.Select(n => n.Id).ToHashSet();
        var parents = nodes.SelectMany(n => n.AllPrerequisiteIds).Where(ids.Contains).ToHashSet();
        var withEdges = nodes.Where(n => parents.Contains(n.Id) || n.AllPrerequisiteIds.Any(ids.Contains)).ToList();

        foreach (var component in GroupIntoComponents(withEdges)
                     .OrderBy(c => branchOrder.IndexOf(c[0].GroupId))
                     .ThenBy(c => c.Min(n => n.Tier)))
        {
            var capstone = component
                .OrderByDescending(n => n.IsCapstone)
                .ThenByDescending(n => n.Tier)
                .First();
            var key = component.Select(n => n.Id).Min(StringComparer.Ordinal)!;
            _lines.Add((key, capstone.Name, component.Select(n => n.Id).ToList()));
        }

        var strays = nodes.Except(withEdges).Select(n => n.Id).ToList();
        if (strays.Count > 0)
            _lines.Add((StrayLineKey, "Standalone", strays));
    }

    private void BuildDrawIndexes()
    {
        foreach (var node in _nodes)
        {
            foreach (var prereq in node.AllPrerequisiteIds)
            {
                if (!_nodeById.ContainsKey(prereq))
                    continue;
                if (!_childrenByParent.TryGetValue(prereq, out var children))
                    _childrenByParent[prereq] = children = new List<FSResearchNodeView>();
                children.Add(node);
            }
        }

        foreach (var group in _nodes.Where(n => n.Proto.ExclusiveGroup != null).GroupBy(n => n.Proto.ExclusiveGroup))
        {
            var members = group.OrderBy(n => _positions[n.Id].X).ToList();
            if (members.Count > 1)
                _exclusiveGroups.Add(members);
        }
    }

    private static List<List<FSResearchNodeView>> GroupIntoComponents(List<FSResearchNodeView> nodes)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var neighbours = nodes.ToDictionary(n => n.Id, _ => new List<string>());
        foreach (var node in nodes)
        {
            foreach (var prereq in node.AllPrerequisiteIds.Where(byId.ContainsKey))
            {
                neighbours[node.Id].Add(prereq);
                neighbours[prereq].Add(node.Id);
            }
        }

        var visited = new HashSet<string>();
        var components = new List<List<FSResearchNodeView>>();
        foreach (var start in nodes)
        {
            if (!visited.Add(start.Id))
                continue;

            var component = new List<FSResearchNodeView>();
            var queue = new Queue<string>();
            queue.Enqueue(start.Id);
            while (queue.TryDequeue(out var id))
            {
                component.Add(byId[id]);
                foreach (var next in neighbours[id])
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            components.Add(component);
        }

        return components;
    }

    private void RefreshNodeStates()
    {
        if (_console is not { } console || !_entityManager.TryGetComponent<TechnologyDatabaseComponent>(console, out var database))
            return;

        _entityManager.TryGetComponent<FSTechDatabaseComponent>(console, out var fsDb);
        var unlocked = database.UnlockedTechnologies.Select(t => t.Id).ToHashSet();
        var unlockedFs = new List<string>();
        if (fsDb != null)
        {
            foreach (var node in fsDb.UnlockedNodes)
            {
                unlocked.Add(node.Id);
                unlockedFs.Add(node.Id);
            }
        }

        var queue = _fsResearch.IsRdOrCaptain
            ? fsDb?.SharedQueue.Select(n => n.Id).ToList() ?? new List<string>()
            : _fsResearch.MyPersonalQueue;
        var myPick = _fsResearch.MyPersonalPickId?.Id;
        var active = fsDb?.ActiveResearch?.Id;

        foreach (var node in _branchById.Values)
        {
            var proto = node.Proto;
            var prereqsMet = proto.Prerequisites.All(unlocked.Contains) && proto.PrerequisiteGroups.All(g => g.Any(unlocked.Contains));
            node.State = unlocked.Contains(proto.ID) ? FSResearchNodeState.Unlocked
                : _fsResearch.IsExclusivelyBlocked(proto, unlockedFs) ? FSResearchNodeState.ExclusivelyBlocked
                : prereqsMet ? FSResearchNodeState.Available
                : FSResearchNodeState.Locked;
            node.IsActiveResearch = active == proto.ID;
            node.IsMyPersonalPick = myPick == proto.ID;
            node.Progress = fsDb?.NodeProgress.GetValueOrDefault(proto.ID) ?? 0;
            node.PersonalContributorCount = fsDb?.PersonalContributorSlots.GetValueOrDefault(proto.ID)?.Count ?? 0;
            node.QueuePosition = queue.IndexOf(proto.ID) + 1;
        }
    }

    private Vector2 CurrentCursor()
        => UserInterfaceManager.MousePositionScaled.Position * UIScale - GlobalPixelPosition;

    private FSResearchNodeView? HitTest(Vector2 local)
    {
        var origin = Origin();
        foreach (var node in _nodes)
        {
            var bounds = NodeBounds(node);
            var box = new UIBox2(origin + (_positions[node.Id] + bounds.TopLeft) * K, origin + (_positions[node.Id] + bounds.BottomRight) * K);
            if (box.Contains(local))
                return node;
        }

        return null;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_leftDown && _scroll != null)
        {
            var delta = UserInterfaceManager.MousePositionScaled.Position - _dragStartCursor;
            if (_dragging || delta.LengthSquared() >= DragThreshold * DragThreshold)
            {
                _dragging = true;
                _scroll.HScroll = _dragStartScroll.X - delta.X;
                _scroll.VScroll = _dragStartScroll.Y - delta.Y;
            }
        }

        var hovered = _dragging ? null : HitTest(CurrentCursor());
        HoveredId = hovered?.Id;
        DefaultCursorShape = hovered != null ? CursorShape.Hand : CursorShape.Arrow;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        HoveredId = null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled || args.Function != EngineKeyFunctions.UIClick)
            return;

        _leftDown = true;
        _dragging = false;
        _dragStartCursor = UserInterfaceManager.MousePositionScaled.Position;
        _dragStartScroll = _scroll == null ? Vector2.Zero : new Vector2(_scroll.HScroll, _scroll.VScroll);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_leftDown && !_dragging && HitTest(CurrentCursor()) is { } node)
        {
            OnNodeSelected?.Invoke(node);
            UserInterfaceManager.ClickSound();
        }

        _leftDown = false;
        _dragging = false;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        ZoomAround(CurrentCursor(), _zoom * MathF.Pow(1.1f, args.Delta.Y));
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var view = UIBox2.FromDimensions(ScrollPixels(), _scroll != null ? _scroll.Size * UIScale : PixelSize);
        view = new UIBox2(view.TopLeft, Vector2.Min(view.BottomRight, (Vector2) PixelSize));
        var time = (float) _timing.RealTime.TotalSeconds;

        if (Blueprint)
            DrawBlueprint(handle, Origin(), view, time);
        else
            DrawSchematic(handle, Origin(), view, time);
    }
}
