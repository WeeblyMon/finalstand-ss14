using System.Linq;
using Content.Client._Shitmed.Choice.UI;
using Content.Client.Guidebook;
using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.Guidebook;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Shitmed.Medical.Surgery;

[UsedImplicitly]
public sealed partial class SurgeryBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color MutedColor = Color.FromHex("#7F8891");
    private static readonly Color HeaderColor = Color.FromHex("#C9B28A");
    private static readonly Color DoneColor = Color.FromHex("#6E9E7E");
    private static readonly Color NextColor = Color.FromHex("#8FE0B0");
    private static readonly Color ReadyColor = Color.FromHex("#C8CDD3");
    private static readonly Color WaitingColor = Color.FromHex("#6B7078");
    private static readonly Color ReasonColor = Color.FromHex("#D9A93A");
    private static readonly Color PanelColor = Color.FromHex("#1B1E23");
    private static readonly Color PanelBorderColor = Color.FromHex("#2E333B");
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(1);
    private static readonly ProtoId<GuideEntryPrototype> SurgeryGuide = "Surgery";

    private readonly SurgerySystem _system;
    private readonly FSSurgeryPlanSystem _plans;
    private readonly FSSurgeryGuideSystem _guide;

    [ViewVariables]
    private SurgeryWindow? _window;
    private SurgeryGuidancePresenter? _guidance;
    private SurgeryDollPresenter? _doll;

    private SurgeryBuiState? _state;
    private EntityUid? _part;
    private EntProtoId? _procedure;
    private bool _showOthers;
    private bool _pickedFirstPart;
    private string? _partsKey;
    private string? _crumbKey;

    private readonly Dictionary<EntityUid, List<EntProtoId>> _seen = new();
    private readonly Dictionary<string, Control> _rows = new();
    private string? _layoutKey;

    private FSSurgeryPlanRow? _next;
    private bool _stepRunning;
    private EntProtoId? _runningStep;
    private TimeSpan _pendingUntil;
    private EntityUid? _pendingStep;
    private TimeSpan _doAllPendingUntil;
    private TimeSpan _nextAutoRefresh;
    private bool _syncingDoAll;

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = _entities.System<SurgerySystem>();
        _plans = _entities.System<FSSurgeryPlanSystem>();
        _guide = _entities.System<FSSurgeryGuideSystem>();
    }

    private bool IsPending => _timing.RealTime < _pendingUntil;

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is SurgeryBuiRefreshMessage)
            Refresh();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not SurgeryBuiState s)
            return;

        _state = s;

        var window = EnsureWindow();
        if (!window.IsOpen)
            window.OpenCentered();

        RebuildParts(s);

        if (_part is { } part
            && (!_entities.TryGetNetEntity(part, out var netPart) || !s.Choices.ContainsKey(netPart.Value)))
        {
            _part = null;
            _procedure = null;
        }

        if (!_pickedFirstPart)
        {
            _pickedFirstPart = true;
            _part = MostUrgentPart(s);
        }

        Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private SurgeryWindow EnsureWindow()
    {
        if (_window != null)
            return _window;

        _window = new SurgeryWindow();
        _window.OnClose += Close;
        _window.Title = Loc.GetString("surgery-ui-window-title");

        _guidance = new SurgeryGuidancePresenter(_entities, _window);
        _doll = new SurgeryDollPresenter(_entities, _window, OnPartPressed, OnMissingPartPressed);

        _window.PerformButton.OnPressed += _ =>
        {
            if (_next is { } next)
                Perform(next.Surgery, next.Step, next.StepEnt);
        };

        _window.DoAllButton.OnToggled += args => OnDoAllToggled(args.Pressed);

        _window.BackButton.OnPressed += _ =>
        {
            _procedure = null;
            Refresh();
        };

        _window.GuideButton.OnPressed += _ =>
            _entities.System<GuidebookSystem>().OpenHelp(new List<ProtoId<GuideEntryPrototype>> { SurgeryGuide });

        _window.OnFrameUpdate += OnFrameUpdate;

        foreach (var panel in new[] { _window.BodyPanel, _window.PlanPanel })
        {
            panel.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PanelColor,
                BorderColor = PanelBorderColor,
                BorderThickness = new Thickness(1),
            };
        }

        return _window;
    }

    private void RebuildParts(SurgeryBuiState state)
    {
        if (_window == null || _doll == null)
            return;

        var options = new List<(NetEntity Net, EntityUid Ent, string Name, ProtoId<OrganCategoryPrototype>? Category)>();
        foreach (var net in state.Choices.Keys)
        {
            if (!_entities.TryGetEntity(net, out var ent))
                continue;

            var category = _entities.GetComponentOrNull<OrganComponent>(ent)?.Category;
            options.Add((net, ent.Value, _entities.GetComponent<MetaDataComponent>(ent.Value).EntityName, category));
        }

        options.Sort((a, b) => Rank(a.Category).CompareTo(Rank(b.Category)));

        var key = string.Join(';', options.Select(o => $"{o.Net.Id}:{string.Join(',', state.Choices[o.Net])}"));
        if (key == _partsKey)
            return;

        _partsKey = key;
        _window.Parts.DisposeAllChildren();
        _doll.Clear();

        foreach (var (net, ent, name, category) in options)
        {
            var surgeries = state.Choices[net];
            if (_doll.TryAdd(category, net, ent, surgeries))
                continue;

            var button = new ChoiceControl();
            button.Set(name, null);
            button.Button.OnPressed += _ => OnPartPressed(net, surgeries);
            _window.Parts.AddChild(button);
        }
    }

    private static int Rank(ProtoId<OrganCategoryPrototype>? category)
    {
        var index = category is { } id ? Array.IndexOf(OrganCategories.Body, id) : -1;
        return index < 0 ? int.MaxValue : index;
    }

    private EntityUid? MostUrgentPart(SurgeryBuiState state)
    {
        EntityUid? best = null;
        var bestRank = int.MaxValue;

        foreach (var net in state.Choices.Keys)
        {
            if (!_entities.TryGetEntity(net, out var part))
                continue;

            foreach (var group in _plans.BuildPlan(Owner, part.Value).Groups)
            {
                if (group.Kind != FSPlanGroupKind.Problem
                    || !_guide.TryGetGoalOrder(group.Surgery.Id, out var rank)
                    || rank >= bestRank)
                    continue;

                bestRank = rank;
                best = part;
            }
        }

        return best;
    }

    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeries)
    {
        var part = _entities.GetEntity(netPart);
        if (_part != part)
        {
            _part = part;
            _procedure = null;
            _showOthers = false;
        }

        Refresh();
    }

    private void OnMissingPartPressed(NetEntity parent, List<EntProtoId> parentSurgeries, EntProtoId attach)
    {
        OnPartPressed(parent, parentSurgeries);
    }

    private void Refresh()
    {
        if (_window == null || _guidance == null || !_window.IsOpen)
            return;

        _guidance.Reset();
        _next = null;

        _doll?.Refresh(_entities.TryGetNetEntity(_part, out var selectedNet) ? selectedNet : null);
        UpdateTitle();
        BuildBreadcrumb();

        if (_player.LocalEntity is not { } user
            || !_entities.TryGetComponent(user, out SurgeryTargetComponent? surgeon)
            || !surgeon.CanOperate)
        {
            ShowPlan(null, null, null);
            _guidance.ShowCannotOperate();
            SyncDoAll(null, false);
            return;
        }

        if (_part is not { } part || !_entities.EntityExists(part))
        {
            ShowPlan(null, null, user);
            _guidance.ShowSelectPrompt(user);
            SyncDoAll(null, false);
            return;
        }

        var plan = _procedure is { } procedure
            ? _plans.BuildProcedure(Owner, part, procedure)
            : _plans.BuildPlan(Owner, part);

        if (_procedure == null)
            Remember(part, plan);

        ShowPlan(part, plan, user);
        ShowGuidance(user, part, plan);
        SyncDoAll(part, _procedure == null && plan.Next != null);
    }

    private void Remember(EntityUid part, FSSurgeryPlan plan)
    {
        if (!_seen.TryGetValue(part, out var seen))
            _seen[part] = seen = new List<EntProtoId>();

        foreach (var group in plan.Groups)
        {
            if (group.Kind == FSPlanGroupKind.Problem && !seen.Contains(group.Surgery))
                seen.Add(group.Surgery);
        }
    }

    private List<EntProtoId> FixedGoals(EntityUid part, FSSurgeryPlan plan)
    {
        var fixedGoals = new List<EntProtoId>();
        if (_procedure != null || !_seen.TryGetValue(part, out var seen))
            return fixedGoals;

        foreach (var goal in _guide.Goals)
        {
            if (seen.Contains(goal) && !plan.Groups.Any(g => g.Kind == FSPlanGroupKind.Problem && g.Surgery == goal))
                fixedGoals.Add(goal);
        }

        return fixedGoals;
    }

    private void ShowPlan(EntityUid? part, FSSurgeryPlan? plan, EntityUid? user)
    {
        if (_window == null)
            return;

        _window.BackButton.Visible = _procedure != null;
        UpdatePlanHeader(part, plan);

        var keys = new List<string>();
        var order = new List<Control>();

        if (part is { } p && plan != null && user is { } u)
        {
            var fixedGoals = FixedGoals(p, plan);
            var fixedShown = false;

            foreach (var group in plan.Groups)
            {
                if (group.Kind == FSPlanGroupKind.Closing && !fixedShown)
                {
                    AddFixed(fixedGoals, keys, order);
                    fixedShown = true;
                }

                AddGroupHeader(group, keys, order);
                foreach (var row in group.Rows)
                    AddStepRow(p, u, row, keys, order);
            }

            if (!fixedShown)
                AddFixed(fixedGoals, keys, order);

            if (_procedure == null)
                AddOthers(p, keys, order);
        }

        var layoutKey = string.Join('|', keys);
        if (layoutKey == _layoutKey)
            return;

        _layoutKey = layoutKey;
        _window.Plan.RemoveAllChildren();

        foreach (var control in order)
            _window.Plan.AddChild(control);

        foreach (var stale in _rows.Keys.Except(keys).ToList())
        {
            _rows[stale].Dispose();
            _rows.Remove(stale);
        }
    }

    private T Cached<T>(string key, List<string> keys, List<Control> order, Func<T> create) where T : Control
    {
        if (!_rows.TryGetValue(key, out var control) || control is not T typed)
        {
            control?.Dispose();
            typed = create();
            _rows[key] = typed;
        }

        keys.Add(key);
        order.Add(typed);
        return typed;
    }

    private void AddGroupHeader(FSSurgeryPlanGroup group, List<string> keys, List<Control> order)
    {
        var label = Cached("h:" + group.Kind + ":" + group.Surgery.Id, keys, order,
            () => new RichTextLabel { Margin = new Thickness(0, 8, 0, 2) });

        var name = Name(group.SurgeryEnt);
        var text = group.Kind switch
        {
            FSPlanGroupKind.Opening => Loc.GetString("surgery-ui-plan-open", ("operation", name)),
            FSPlanGroupKind.Closing => Loc.GetString("surgery-ui-plan-close", ("operation", name)),
            _ => name,
        };

        var msg = new FormattedMessage();
        msg.PushColor(group.Done ? DoneColor : HeaderColor);
        msg.AddText(group.Done ? "✓ " + text : text);
        msg.Pop();

        if (!group.Done && group.Blocker is { } blocker)
        {
            msg.PushNewline();
            msg.PushColor(ReasonColor);
            msg.AddText(Loc.GetString("surgery-ui-plan-waiting", ("reason", Loc.GetString(blocker))));
            msg.Pop();
        }

        label.SetMessage(msg);
    }

    private void AddFixed(List<EntProtoId> fixedGoals, List<string> keys, List<Control> order)
    {
        foreach (var goal in fixedGoals)
        {
            if (_system.GetSingleton(goal) is not { } surgery)
                continue;

            var label = Cached("f:" + goal.Id, keys, order, () => new RichTextLabel { Margin = new Thickness(0, 8, 0, 2) });

            var msg = new FormattedMessage();
            msg.PushColor(DoneColor);
            msg.AddText("✓ " + Loc.GetString("surgery-ui-plan-fixed", ("operation", Name(surgery))));
            msg.Pop();
            label.SetMessage(msg);
        }
    }

    private void AddStepRow(EntityUid part, EntityUid user, FSSurgeryPlanRow row, List<string> keys, List<Control> order)
    {
        var button = Cached("s:" + row.Surgery.Id + "/" + row.Step.Id, keys, order, () =>
        {
            var created = new SurgeryStepButton { Margin = new Thickness(14, 0, 0, 1) };
            created.Button.OnPressed += _ => Perform(created.SurgeryId, created.StepId, created.Step);
            return created;
        });

        button.Step = row.StepEnt;
        button.StepId = row.Step;
        button.SurgeryId = row.Surgery;

        var running = _stepRunning && _runningStep == row.Step;
        var (glyph, colour) = row.State switch
        {
            FSPlanRowState.Done => ("✓ ", DoneColor),
            FSPlanRowState.Next => ("▶ ", NextColor),
            FSPlanRowState.Ready => ("• ", ReadyColor),
            _ => ("· ", WaitingColor),
        };

        var msg = new FormattedMessage();
        msg.PushColor(running ? NextColor : colour);
        msg.AddText(glyph + Name(row.StepEnt));
        msg.Pop();

        var enabled = false;
        string? reason = null;

        if (running)
        {
            msg.PushColor(MutedColor);
            msg.AddText("   " + Loc.GetString("surgery-ui-step-running"));
            msg.Pop();
        }
        else if (row.State is FSPlanRowState.Next or FSPlanRowState.Ready)
        {
            var found = _system.TryFindStepTool(user, Owner, part, row.StepEnt, out var tool, out var popup, out var invalid, out _);

            msg.PushColor(MutedColor);
            msg.AddText("   " + Details(user, part, row, found ? tool : null));
            msg.Pop();

            if (!found)
                reason = _system.DescribeRefusal(invalid, row.Step, popup);

            enabled = found && !_stepRunning && !IsPending;
        }

        if (reason != null)
        {
            msg.PushNewline();
            msg.PushColor(ReasonColor);
            msg.AddText("    " + reason);
            msg.Pop();
        }

        button.Set(msg, row.State == FSPlanRowState.Done ? null : Icon(row.StepEnt));
        button.Button.Disabled = !enabled;
    }

    private void AddOthers(EntityUid part, List<string> keys, List<Control> order)
    {
        var electives = Electives(part);
        if (electives.Count == 0)
            return;

        var toggle = Cached("others", keys, order, () =>
        {
            var created = new Button
            {
                StyleClasses = { "ButtonSquare" },
                HorizontalExpand = true,
                Margin = new Thickness(0, 14, 0, 2),
                ToolTip = Loc.GetString("surgery-ui-plan-other-tooltip"),
            };
            created.OnPressed += _ =>
            {
                _showOthers = !_showOthers;
                Refresh();
            };
            return created;
        });

        toggle.Text = _showOthers
            ? Loc.GetString("surgery-ui-plan-other-hide")
            : Loc.GetString("surgery-ui-plan-other", ("count", electives.Count));

        if (!_showOthers)
            return;

        foreach (var (id, surgery) in electives)
        {
            var choice = Cached("o:" + id.Id, keys, order, () =>
            {
                var created = new ChoiceControl { Margin = new Thickness(14, 0, 0, 1) };
                created.Button.OnPressed += _ =>
                {
                    _procedure = id;
                    _showOthers = false;
                    Refresh();
                };
                return created;
            });

            choice.Set(Name(surgery), null);
        }
    }

    private List<(EntProtoId Id, EntityUid Surgery)> Electives(EntityUid part)
    {
        var electives = new List<(EntProtoId, EntityUid)>();
        if (_state == null
            || !_entities.TryGetNetEntity(part, out var net)
            || !_state.Choices.TryGetValue(net.Value, out var choices))
            return electives;

        foreach (var id in _guide.Electives)
        {
            if (choices.Contains(id) && _system.GetSingleton(id) is { } surgery)
                electives.Add((id, surgery));
        }

        return electives;
    }

    private string Details(EntityUid user, EntityUid part, FSSurgeryPlanRow row, EntityUid? tool)
    {
        var seconds = Math.Round(_system.GetEffectiveStepDuration(user, Owner, part, row.Step, row.StepEnt), 1);

        if (tool is not { } found)
        {
            var names = _system.GetStepToolNames(row.StepEnt);
            return names.Count == 0
                ? Loc.GetString("surgery-ui-step-details-time", ("seconds", seconds))
                : Loc.GetString("surgery-ui-step-details-needs", ("tool", string.Join(", ", names)), ("seconds", seconds));
        }

        if (found == user)
            return Loc.GetString("surgery-ui-step-details-time", ("seconds", seconds));

        return Loc.GetString("surgery-ui-step-details",
            ("tool", Name(found)),
            ("where", Loc.GetString(_system.DescribeToolPlace(user, found))),
            ("seconds", seconds));
    }

    private void ShowGuidance(EntityUid user, EntityUid part, FSSurgeryPlan plan)
    {
        if (_guidance == null)
            return;

        if (_stepRunning)
        {
            _guidance.ShowWorking();
            return;
        }

        if (IsPending && _pendingStep is { } pending)
        {
            _guidance.ShowStarting(pending);
            return;
        }

        if (plan.Next is { } next)
        {
            _next = next;

            if (_system.TryFindStepTool(user, Owner, part, next.StepEnt, out var tool, out var popup, out var reason, out _))
                _guidance.ShowNext(next.StepEnt, Details(user, part, next, tool), user);
            else
                _guidance.ShowBlocked(next.StepEnt, _system.DescribeRefusal(reason, next.Step, popup));

            return;
        }

        foreach (var group in plan.Groups)
        {
            if (group.Kind == FSPlanGroupKind.Problem && !group.Done && group.Blocker is { } blocker)
            {
                _guidance.ShowWaiting(Name(group.SurgeryEnt), Loc.GetString(blocker));
                return;
            }
        }

        _guidance.ShowNothing(FixedGoals(part, plan).Count > 0);
    }

    private void Perform(EntProtoId surgery, EntProtoId step, EntityUid stepEnt)
    {
        if (_part is not { } part || !_entities.TryGetNetEntity(part, out var net))
            return;

        SendPredictedMessage(new SurgeryStepChosenBuiMsg(net.Value, surgery, step, false));
        _pendingUntil = _timing.RealTime + PendingTimeout;
        _pendingStep = stepEnt;
        Refresh();
    }

    private void OnDoAllToggled(bool pressed)
    {
        if (_syncingDoAll || _part is not { } part || !_entities.TryGetNetEntity(part, out var net))
            return;

        _doAllPendingUntil = _timing.RealTime + PendingTimeout;
        SendMessage(new FSSurgeryAutoMessage(net.Value, pressed));
    }

    private void SyncDoAll(EntityUid? part, bool available)
    {
        if (_window == null)
            return;

        var on = part != null
                 && _player.LocalEntity is { } user
                 && _entities.TryGetComponent(user, out FSSurgeryAutoComponent? auto)
                 && auto.Patient == Owner
                 && auto.Part == part;

        if (_timing.RealTime >= _doAllPendingUntil)
        {
            _syncingDoAll = true;
            _window.DoAllButton.Pressed = on;
            _syncingDoAll = false;
        }

        _window.DoAllButton.Disabled = !_window.DoAllButton.Pressed && !available;
    }

    private void OnFrameUpdate()
    {
        var running = DrawStepProgress();
        var now = _timing.RealTime;

        if (running == _stepRunning && now < _nextAutoRefresh)
            return;

        if (running)
            _pendingUntil = TimeSpan.Zero;

        _stepRunning = running;
        _nextAutoRefresh = now + AutoRefreshInterval;
        Refresh();
    }

    private bool DrawStepProgress()
    {
        if (_window == null)
            return false;

        if (_player.LocalEntity is not { } user
            || !_entities.TryGetComponent(user, out DoAfterComponent? doAfters))
        {
            _window.StepProgress.Visible = false;
            _runningStep = null;
            return false;
        }

        var now = _timing.CurTime;
        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (doAfter.Cancelled || doAfter.Completed || doAfter.Args.Event is not SurgeryDoAfterEvent surgeryEvent)
                continue;

            var length = doAfter.Args.Delay.TotalSeconds;
            if (length <= 0)
                continue;

            _window.StepProgress.Visible = true;
            _window.StepProgress.Value = Math.Clamp((float) ((now - doAfter.StartTime).TotalSeconds / length), 0f, 1f);
            _runningStep = surgeryEvent.Step;
            return true;
        }

        _window.StepProgress.Visible = false;
        _runningStep = null;
        return false;
    }

    private void UpdatePlanHeader(EntityUid? part, FSSurgeryPlan? plan)
    {
        if (_window == null)
            return;

        var msg = new FormattedMessage();
        msg.PushColor(MutedColor);

        if (part is not { } p || plan == null)
        {
            msg.AddText(Loc.GetString("surgery-ui-column-plan"));
        }
        else if (_procedure is { } procedure && _system.GetSingleton(procedure) is { } surgery)
        {
            msg.AddText(Loc.GetString("surgery-ui-plan-procedure", ("part", Name(p)), ("operation", Name(surgery))));
        }
        else
        {
            var problems = plan.Groups.Count(g => g.Kind == FSPlanGroupKind.Problem);
            msg.AddText(Loc.GetString(plan.IsOpen ? "surgery-ui-plan-header-open" : "surgery-ui-plan-header",
                ("part", Name(p)),
                ("problems", problems)));
        }

        msg.Pop();
        _window.PlanHeader.SetMessage(msg);
    }

    private void UpdateTitle()
    {
        if (_window == null)
            return;

        if (_part is not { } part || !_entities.EntityExists(part))
        {
            _window.Title = Loc.GetString("surgery-ui-window-title");
            return;
        }

        _window.Title = _procedure is { } procedure && _system.GetSingleton(procedure) is { } surgery
            ? Loc.GetString("surgery-ui-window-title-operation", ("part", Name(part)), ("operation", Name(surgery)))
            : Loc.GetString("surgery-ui-window-title-part", ("part", Name(part)));
    }

    private void BuildBreadcrumb()
    {
        if (_window == null)
            return;

        var key = $"{_part?.Id};{_procedure?.Id}";
        if (key == _crumbKey)
            return;

        _crumbKey = key;
        _window.Breadcrumb.DisposeAllChildren();

        AddCrumb(Loc.GetString("surgery-ui-crumb-patient"), _part != null, () =>
        {
            _part = null;
            _procedure = null;
            Refresh();
        });

        if (_part is not { } part || !_entities.EntityExists(part))
            return;

        AddCrumb(Name(part), _procedure != null, () =>
        {
            _procedure = null;
            Refresh();
        });

        if (_procedure is { } procedure && _system.GetSingleton(procedure) is { } surgery)
            AddCrumb(Name(surgery), false, null);
    }

    private void AddCrumb(string text, bool navigable, Action? onPressed)
    {
        if (_window == null)
            return;

        if (_window.Breadcrumb.ChildCount > 0)
        {
            _window.Breadcrumb.AddChild(new Label
            {
                Text = " > ",
                VerticalAlignment = Control.VAlignment.Center,
                Modulate = MutedColor,
            });
        }

        if (!navigable || onPressed == null)
        {
            _window.Breadcrumb.AddChild(new Label
            {
                Text = text,
                VerticalAlignment = Control.VAlignment.Center,
            });
            return;
        }

        var button = new Button
        {
            Text = text,
            StyleClasses = { "ButtonSquare" },
        };
        button.OnPressed += _ => onPressed();
        _window.Breadcrumb.AddChild(button);
    }

    private string Name(EntityUid uid)
    {
        return _entities.GetComponent<MetaDataComponent>(uid).EntityName;
    }

    private Texture? Icon(EntityUid step)
    {
        return _entities.GetComponentOrNull<SpriteComponent>(step)?.Icon?.Default;
    }
}
