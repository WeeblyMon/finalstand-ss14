
using System;
using System.Linq;
using Content.Shared._FinalStand.Medical;
using Robust.Shared.Prototypes;
using Content.Client._Shitmed.Choice.UI;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Stylesheets;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared.Body.Components;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Robust.Shared.Timing;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Shitmed.Medical.Surgery;

[UsedImplicitly]
public sealed partial class SurgeryBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color MutedColor = Color.FromHex("#7F8891");
    private static readonly Color StepCompleteColor = new(0.55f, 0.55f, 0.55f);
    private static readonly Color StepLockedColor = new(0.40f, 0.40f, 0.40f);
    private static readonly Color OperationNextColor = Color.FromHex("#8FE0B0");
    private static readonly Color OperationAvailableColor = new(0.72f, 0.72f, 0.72f);
    private static readonly Color OperationOutOfFocusColor = new(0.45f, 0.45f, 0.45f);
    private static readonly Color OperationUnavailableColor = new(0.32f, 0.32f, 0.36f);

    private readonly Dictionary<NetEntity, List<EntProtoId>> _unavailable = new();

    private static readonly (SurgeryFocus Focus, string Loc)[] Filters =
    {
        (SurgeryFocus.All, "surgery-ui-filter-all"),
        (SurgeryFocus.Bleeding, "surgery-ui-filter-bleeding"),
        (SurgeryFocus.Wounds, "surgery-ui-filter-wounds"),
        (SurgeryFocus.Bones, "surgery-ui-filter-bones"),
        (SurgeryFocus.Organs, "surgery-ui-filter-organs"),
    };

    private readonly Dictionary<SurgeryFocus, Button> _filterButtons = new();
    private readonly HashSet<EntityUid> _neededAccess = new();
    private SurgeryOperationClassifier? _classifier;
    private SurgeryFocus _focus = SurgeryFocus.All;

    private readonly SurgerySystem _system;
    [ViewVariables]
    private SurgeryWindow? _window;
    private SurgeryGuidancePresenter? _guidance;
    private SurgeryDollPresenter? _dollPresenter;

    private EntityUid? _part;
    private bool _isBody;
    private (EntityUid Ent, EntProtoId Proto)? _surgery;
    private readonly List<EntProtoId> _previousSurgeries = new();

    private string? _partsKey;
    private string? _surgeriesKey;
    private string? _crumbKey;
    private (NetEntity Part, EntProtoId Surgery)? _stepsKey;

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) => _system = _entities.System<SurgerySystem>();

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window is null
            || message is not SurgeryBuiRefreshMessage)
            return;

        RefreshUI();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not SurgeryBuiState s)
            return;

        Update(s);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private void Update(SurgeryBuiState state)
    {
        if (_window == null)
        {
            _window = new SurgeryWindow();
            _window.OnClose += Close;
            _window.Title = Loc.GetString("surgery-ui-window-title");

            _guidance = new SurgeryGuidancePresenter(_entities, _system, _window);
            _dollPresenter = new SurgeryDollPresenter(_entities, _window, OnPartPressed);
            _classifier = new SurgeryOperationClassifier(_entities, IoCManager.Resolve<IPrototypeManager>());
            BuildFilters();

            _window.PerformButton.OnPressed += _ =>
            {
                if (_guidance?.NextStep is { } next)
                    SendPredictedMessage(new SurgeryStepChosenBuiMsg(next.NetPart, next.SurgeryId, next.StepId, _isBody));
            };

            _window.OnFrameUpdate += UpdateStepProgress;

            foreach (var panel in new[] { _window.BodyPanel, _window.OperationsPanel, _window.ProcedurePanel })
            {
                panel.PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#1B1E23"),
                    BorderColor = Color.FromHex("#2E333B"),
                    BorderThickness = new Thickness(1),
                };
            }
        }

        if (!_window.IsOpen)
            _window.OpenCentered();

        _unavailable.Clear();
        foreach (var (part, ids) in state.Unavailable)
            _unavailable[part] = ids;

        var oldSurgery = _surgery;
        var oldPart = _part;

        var options = new List<(NetEntity netEntity, EntityUid entity, string Name, ProtoId<OrganCategoryPrototype>? Category)>();
        foreach (var choice in state.Choices.Keys)
            if (_entities.TryGetEntity(choice, out var ent))
            {
                if (_entities.TryGetComponent(ent, out OrganComponent? part))
                    options.Add((choice, ent.Value, _entities.GetComponent<MetaDataComponent>(ent.Value).EntityName, part.Category));
                else if (_entities.TryGetComponent(ent, out BodyComponent? body))
                    options.Add((choice, ent.Value, _entities.GetComponent<MetaDataComponent>(ent.Value).EntityName, null));
            }

        options.Sort((a, b) =>
        {
            int GetScore(ProtoId<OrganCategoryPrototype>? category)
            {
                var index = category is { } id ? Array.IndexOf(OrganCategories.Body, id) : -1;
                return index < 0 ? int.MaxValue : index;
            }

            return GetScore(a.Category) - GetScore(b.Category);
        });

        var partsKey = string.Join(';',
            options.Select(o => $"{o.netEntity.Id}:{string.Join(',', state.Choices[o.netEntity])}"));
        if (partsKey != _partsKey)
        {
            _partsKey = partsKey;
            _window.Parts.DisposeAllChildren();
            _dollPresenter!.Clear();

            foreach (var (netEntity, entity, partName, category) in options)
            {
                var surgeries = state.Choices[netEntity];
                if (_dollPresenter.TryAdd(category, netEntity, entity, surgeries))
                    continue;

                var partButton = new ChoiceControl();

                partButton.Set(partName, null);
                partButton.Button.OnPressed += _ => OnPartPressed(netEntity, surgeries);

                _window.Parts.AddChild(partButton);
            }
        }

        var restored = false;
        if (oldPart != null)
        {
            foreach (var (netEntity, entity, _, _) in options)
            {
                if (entity != oldPart)
                    continue;

                RebuildOperations(netEntity, state.Choices[netEntity]);

                if (oldSurgery is { } selected
                    && _system.GetSingleton(selected.Proto) is { } surgery
                    && _entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
                {
                    OnSurgeryPressed((surgery, surgeryComp), netEntity, selected.Proto);
                }
                else
                {
                    OnPartPressed(netEntity, state.Choices[netEntity]);
                }

                restored = true;
                break;
            }
        }

        if (!restored)
        {
            ClearSelection();
            UpdateHeader();
            RefreshUI();
        }
    }

    private void ClearSelection()
    {
        _part = null;
        _isBody = false;
        _surgery = null;
        _previousSurgeries.Clear();

        if (_stepsKey == null)
            return;

        _stepsKey = null;
        _window?.Steps.DisposeAllChildren();
    }

    private void AddStep(EntProtoId stepId, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null
            || _system.GetSingleton(stepId) is not { } step)
            return;

        var stepName = new FormattedMessage();
        stepName.AddText(_entities.GetComponent<MetaDataComponent>(step).EntityName);
        var stepButton = new SurgeryStepButton
        {
            Step = step,
            StepId = stepId,
            NetPart = netPart,
            SurgeryId = surgeryId,
        };
        stepButton.Button.OnPressed += _ => SendPredictedMessage(new SurgeryStepChosenBuiMsg(netPart, surgeryId, stepId, _isBody));

        _window.Steps.AddChild(stepButton);
    }

    private void OnSurgeryPressed(Entity<SurgeryComponent> surgery, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _isBody = _entities.HasComponent<BodyComponent>(_part);
        _surgery = (surgery, surgeryId);

        if (_stepsKey != (netPart, surgeryId))
        {
            _stepsKey = (netPart, surgeryId);
            _window.Steps.DisposeAllChildren();

            if (surgery.Comp.Requirement is { } requirementId && _system.GetSingleton(requirementId) is { } requirement)
            {
                var label = new ChoiceControl();
                label.Button.OnPressed += _ =>
                {
                    _previousSurgeries.Add(surgeryId);

                    if (_entities.TryGetComponent(requirement, out SurgeryComponent? requirementComp))
                        OnSurgeryPressed((requirement, requirementComp), netPart, requirementId);
                };

                var msg = new FormattedMessage();
                var surgeryName = _entities.GetComponent<MetaDataComponent>(requirement).EntityName;
                msg.AddMarkup($"[bold]{Loc.GetString("surgery-ui-window-require")}: {surgeryName}[/bold]");
                label.Set(msg, null);

                _window.Steps.AddChild(label);
                _window.Steps.AddChild(new HSeparator { Margin = new Thickness(0, 0, 0, 1) });
            }

            foreach (var stepId in surgery.Comp.Steps)
                AddStep(stepId, netPart, surgeryId);
        }

        UpdateHeader();
        RefreshUI();
    }

    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        var part = _entities.GetEntity(netPart);

        if (_part != part)
        {
            _surgery = null;
            _previousSurgeries.Clear();

            if (_stepsKey != null)
            {
                _stepsKey = null;
                _window.Steps.DisposeAllChildren();
            }
        }

        _part = part;
        _isBody = _entities.HasComponent<BodyComponent>(part);

        RebuildOperations(netPart, surgeryIds);

        UpdateHeader();
        RefreshUI();
    }

    private void RebuildOperations(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        var unavailableIds = _unavailable.GetValueOrDefault(netPart) ?? new List<EntProtoId>();

        var key = $"{netPart.Id}:{string.Join(',', surgeryIds)}|{string.Join(',', unavailableIds)}";
        if (_surgeriesKey == key)
            return;

        _surgeriesKey = key;
        _window.Surgeries.DisposeAllChildren();

        var surgeries = new List<(Entity<SurgeryComponent> Ent, EntProtoId Id, string Name, bool Unavailable)>();
        foreach (var (surgeryId, unavailable) in surgeryIds.Select(id => (id, false))
                     .Concat(unavailableIds.Select(id => (id, true))))
        {
            if (_system.GetSingleton(surgeryId) is not { } surgery ||
                !_entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
            {
                continue;
            }

            var name = _entities.GetComponent<MetaDataComponent>(surgery).EntityName;
            surgeries.Add(((surgery, surgeryComp), surgeryId, name, unavailable));
        }

        surgeries.Sort((a, b) =>
        {
            // Unavailable operations sink below everything the doctor can actually start.
            if (a.Unavailable != b.Unavailable)
                return a.Unavailable ? 1 : -1;

            var priority = a.Ent.Comp.Priority.CompareTo(b.Ent.Comp.Priority);
            if (priority != 0)
                return priority;

            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        foreach (var surgery in surgeries)
        {
            var surgeryButton = new SurgeryOperationButton
            {
                Surgery = surgery.Ent.Owner,
                SurgeryId = surgery.Id,
                OperationName = surgery.Name,
                Unavailable = surgery.Unavailable,
            };
            surgeryButton.Set(surgery.Name, null);

            if (surgery.Unavailable)
                surgeryButton.Button.Disabled = true;
            else
                surgeryButton.Button.OnPressed += _ => OnSurgeryPressed(surgery.Ent, netPart, surgery.Id);

            _window.Surgeries.AddChild(surgeryButton);
        }
    }

    private void RefreshUI()
    {
        if (_window == null || _guidance == null || !_window.IsOpen)
            return;

        _guidance.Reset();

        if (_part == null)
        {
            _guidance.ShowSelectPrompt(_player.LocalEntity);
            return;
        }

        if (!_entities.TryGetComponent(_player.LocalEntity, out SurgeryTargetComponent? surgeryComp)
            || !surgeryComp.CanOperate
            || _player.LocalEntity is not { } user)
        {
            _guidance.ShowCannotOperate();
            return;
        }

        var recommended = RefreshOperations(user);
        var selectedNet = _entities.TryGetNetEntity(_part, out var part) ? part : null;

        if (!_entities.HasComponent<SurgeryComponent>(_surgery?.Ent))
        {
            _guidance.ShowChooseOperation(recommended, ActiveFocusName(), user);
            _dollPresenter?.Refresh(selectedNet);
            return;
        }

        var next = _system.GetNextStep(Owner, _part.Value, _surgery.Value.Ent, user);
        SurgeryStepButton? nextButton = null;
        var i = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not SurgeryStepButton stepButton)
                continue;

            var status = StepStatus.Incomplete;
            if (next == null)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i > -next.Value.Step - 1)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i <= -next.Value.Step - 1)
                status = StepStatus.Next;
            else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
                status = StepStatus.Incomplete;
            else if (next.Value.Step == i)
                status = StepStatus.Next;
            else if (i < next.Value.Step)
                status = StepStatus.Complete;

            stepButton.Button.Disabled = status != StepStatus.Next;

            var stepName = new FormattedMessage();
            stepName.AddText(status switch
            {
                StepStatus.Complete => "✓  ",
                StepStatus.Next => "▶  ",
                _ => "·  ",
            });
            stepName.AddText(_entities.GetComponent<MetaDataComponent>(stepButton.Step).EntityName);

            var duration = _system.GetStepDuration(stepButton.Step);
            if (duration > 0f)
            {
                stepName.PushColor(MutedColor);
                stepName.AddText($"   {duration:0.#}s");
                stepName.Pop();
            }

            stepButton.Button.Modulate = status switch
            {
                StepStatus.Complete => StepCompleteColor,
                StepStatus.Next => Color.White,
                _ => StepLockedColor,
            };

            if (status == StepStatus.Next)
            {
                nextButton ??= stepButton;
                stepButton.ToolTip = _system.CanPerformStepWithAvailable(user, Owner, _part.Value, stepButton.Step, out var popup)
                    ? null
                    : popup;
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(stepButton.Step)?.Icon?.Default;
            stepButton.Set(stepName, texture);
            i++;
        }

        var blockedBy = next != null && next.Value.Surgery.Owner != _surgery.Value.Ent
            ? next.Value.Surgery.Owner
            : (EntityUid?) null;

        var unnecessaryAccess = _classifier != null
                                && _classifier.IsAccess(_surgery.Value.Proto.Id)
                                && !_neededAccess.Contains(_surgery.Value.Ent);

        _guidance.Show(nextButton, next != null, user, Owner, _part.Value,
            _surgery.Value.Ent, blockedBy, unnecessaryAccess);

        _dollPresenter?.Refresh(selectedNet);
    }

    private void UpdateStepProgress()
    {
        if (_window == null)
            return;

        if (_player.LocalEntity is not { } user
            || !_entities.TryGetComponent(user, out DoAfterComponent? doAfters))
        {
            _window.StepProgress.Visible = false;
            return;
        }

        var now = _timing.CurTime;
        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (doAfter.Cancelled || doAfter.Completed || doAfter.Args.Event is not SurgeryDoAfterEvent)
                continue;

            var length = doAfter.Args.Delay.TotalSeconds;
            if (length <= 0)
                continue;

            _window.StepProgress.Visible = true;
            _window.StepProgress.Value = Math.Clamp((float)((now - doAfter.StartTime).TotalSeconds / length), 0f, 1f);
            return;
        }

        _window.StepProgress.Visible = false;
    }

    private void BuildFilters()
    {
        if (_window == null)
            return;

        for (var i = 0; i < Filters.Length; i++)
        {
            var (focus, loc) = Filters[i];

            var style = i == 0
                ? StyleClass.ButtonOpenRight
                : i == Filters.Length - 1
                    ? StyleClass.ButtonOpenLeft
                    : StyleClass.ButtonOpenBoth;

            var button = new Button
            {
                Text = Loc.GetString(loc),
                StyleClasses = { style },
                ToggleMode = true,
                Pressed = focus == _focus,
                HorizontalExpand = true,
            };

            button.OnPressed += _ => SetFocus(focus);

            _filterButtons[focus] = button;
            _window.OperationFilters.AddChild(button);
        }
    }

    private string? ActiveFocusName()
    {
        if (_focus == SurgeryFocus.All)
            return null;

        foreach (var (focus, loc) in Filters)
        {
            if (focus == _focus)
                return Loc.GetString(loc);
        }

        return null;
    }

    private void SetFocus(SurgeryFocus focus)
    {
        _focus = focus;

        foreach (var (candidate, button) in _filterButtons)
            button.Pressed = candidate == focus;

        RefreshUI();
    }

    private string? RefreshOperations(EntityUid user)
    {
        if (_window == null || _part == null || _classifier == null)
            return null;

        SurgeryOperationButton? recommended = null;
        var bestUrgency = int.MaxValue;
        _neededAccess.Clear();

        var states = new List<(SurgeryOperationButton Op, bool Complete, bool Blocked, bool InFocus)>();

        foreach (var child in _window.Surgeries.Children)
        {
            if (child is not SurgeryOperationButton op)
                continue;

            if (op.Unavailable)
            {
                states.Add((op, false, false, false));
                continue;
            }

            var next = _system.GetNextStep(Owner, _part.Value, op.Surgery, user);
            var complete = next == null;
            var blocked = !complete && next!.Value.Surgery.Owner != op.Surgery;
            var inFocus = _classifier.Matches(op.Surgery, _focus);

            states.Add((op, complete, blocked, inFocus));

            if (complete || !inFocus)
                continue;

            if (_classifier.IsAccess(op.SurgeryId.Id))
                continue;

            if (blocked)
                _neededAccess.Add(next!.Value.Surgery.Owner);

            var urgency = _classifier.UrgencyOf(op.Surgery, op.SurgeryId.Id);
            if (urgency >= bestUrgency)
                continue;

            bestUrgency = urgency;
            recommended = op;
        }

        foreach (var (op, complete, blocked, inFocus) in states)
        {
            string glyph;
            Color colour;

            if (op.Unavailable)
            {
                glyph = "✕  ";
                colour = OperationUnavailableColor;
            }
            else if (complete)
            {
                glyph = "✓  ";
                colour = StepCompleteColor;
            }
            else if (blocked)
            {
                glyph = "·  ";
                colour = StepLockedColor;
            }
            else if (op == recommended)
            {
                glyph = "▶  ";
                colour = OperationNextColor;
            }
            else
            {
                glyph = "•  ";
                colour = inFocus ? OperationAvailableColor : OperationOutOfFocusColor;
            }

            var msg = new FormattedMessage();
            msg.AddText(glyph);
            msg.AddText(op.OperationName);

            op.Set(msg, null);
            op.Button.Modulate = colour;
        }

        return recommended?.OperationName;
    }

    private void UpdateHeader()
    {
        if (_window == null)
            return;

        BuildBreadcrumb();
        _dollPresenter?.Refresh(_entities.TryGetNetEntity(_part, out var netPart) ? netPart : null);

        if (_entities.TryGetComponent(_part, out MetaDataComponent? partMeta) &&
            _entities.TryGetComponent(_surgery?.Ent, out MetaDataComponent? surgeryMeta))
            _window.Title = $"Surgery - {partMeta.EntityName}, {surgeryMeta.EntityName}";
        else if (partMeta != null)
            _window.Title = $"Surgery - {partMeta.EntityName}";
        else
            _window.Title = "Surgery";
    }

    private void BuildBreadcrumb()
    {
        if (_window == null)
            return;

        var key = $"{_part?.Id};{_surgery?.Proto.Id};{string.Join(',', _previousSurgeries)}";
        if (key == _crumbKey)
            return;

        _crumbKey = key;
        _window.Breadcrumb.DisposeAllChildren();

        AddCrumb(Loc.GetString("surgery-ui-crumb-patient"), _part != null, () =>
        {
            ClearSelection();
            UpdateHeader();
            RefreshUI();
        });

        if (_part == null)
            return;

        var partName = _entities.GetComponent<MetaDataComponent>(_part.Value).EntityName;
        AddCrumb(partName, _surgery != null, () =>
        {
            if (!_entities.TryGetNetEntity(_part, out var netPart)
                || State is not SurgeryBuiState s
                || !s.Choices.TryGetValue(netPart.Value, out var surgeries))
                return;

            _surgery = null;
            _previousSurgeries.Clear();

            if (_stepsKey != null)
            {
                _stepsKey = null;
                _window.Steps.DisposeAllChildren();
            }

            OnPartPressed(netPart.Value, surgeries);
        });

        for (var i = 0; i < _previousSurgeries.Count; i++)
        {
            var index = i;
            var protoId = _previousSurgeries[i];
            if (_system.GetSingleton(protoId) is not { } ent)
                continue;

            var name = _entities.GetComponent<MetaDataComponent>(ent).EntityName;
            AddCrumb(name, true, () =>
            {
                if (!_entities.TryGetNetEntity(_part, out var netPart)
                    || !_entities.TryGetComponent(ent, out SurgeryComponent? comp))
                    return;

                _previousSurgeries.RemoveRange(index, _previousSurgeries.Count - index);
                OnSurgeryPressed((ent, comp), netPart.Value, protoId);
            });
        }

        if (_surgery is { } current)
            AddCrumb(_entities.GetComponent<MetaDataComponent>(current.Ent).EntityName, false, null);
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

    private enum StepStatus
    {
        Next,
        Complete,
        Incomplete
    }
}
