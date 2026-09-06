// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Kayzel <43700376+KayzelW@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Trest <144359854+trest100@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 kurokoTurbo <92106367+kurokoTurbo@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    private static readonly (SurgeryFocus Focus, string Loc)[] Filters =
    {
        (SurgeryFocus.All, "surgery-ui-filter-all"),
        (SurgeryFocus.Bleeding, "surgery-ui-filter-bleeding"),
        (SurgeryFocus.Wounds, "surgery-ui-filter-wounds"),
        (SurgeryFocus.Bones, "surgery-ui-filter-bones"),
        (SurgeryFocus.Organs, "surgery-ui-filter-organs"),
    };

    private readonly Dictionary<SurgeryFocus, Button> _filterButtons = new();
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

    // Rebuild keys. The steps key carries the part as well as the surgery, because step buttons
    // capture netPart in their closures - the same operation on a different limb must rebuild or it
    // would send the message to the old limb.
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
            _classifier = new SurgeryOperationClassifier(_entities);
            BuildFilters();

            _window.PerformButton.OnPressed += _ =>
            {
                if (_guidance?.NextStep is { } next)
                    SendPredictedMessage(new SurgeryStepChosenBuiMsg(next.NetPart, next.SurgeryId, next.StepId, _isBody));
            };

            _window.OnFrameUpdate += UpdateStepProgress;

            // The columns were reading as one flat sheet without a background behind each.
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

        // Opened before anything below can call RefreshUI, which no-ops on a closed window.
        if (!_window.IsOpen)
            _window.OpenCentered();

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

        // Keyed on the available operations too, not just the limbs - a completed step can make a new
        // operation available, and the part buttons capture their surgery list in a closure.
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

        // The part is gone - amputated, most likely. Keeping the selection would let the player carry
        // on operating on something no longer attached.
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

        // The step list is static for a given part and surgery; only status changes, and RefreshUI
        // owns that.
        if (_stepsKey != (netPart, surgeryId))
        {
            _stepsKey = (netPart, surgeryId);
            _window.Steps.DisposeAllChildren();

            // This apparently does not consider if theres multiple surgery requirements in one surgery. Maybe thats fine.
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

        // Choosing a limb ends whatever operation was running on the previous one. Without this the
        // Procedure column keeps describing a limb the surgeon has already moved off.
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

        var key = $"{netPart.Id}:{string.Join(',', surgeryIds)}";
        if (_surgeriesKey != key)
        {
            _surgeriesKey = key;
            _window.Surgeries.DisposeAllChildren();

            var surgeries = new List<(Entity<SurgeryComponent> Ent, EntProtoId Id, string Name)>();
            foreach (var surgeryId in surgeryIds)
            {
                if (_system.GetSingleton(surgeryId) is not { } surgery ||
                    !_entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
                {
                    continue;
                }

                var name = _entities.GetComponent<MetaDataComponent>(surgery).EntityName;
                surgeries.Add(((surgery, surgeryComp), surgeryId, name));
            }

            surgeries.Sort((a, b) =>
            {
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
                };
                surgeryButton.Set(surgery.Name, null);

                surgeryButton.Button.OnPressed += _ => OnSurgeryPressed(surgery.Ent, netPart, surgery.Id);
                _window.Surgeries.AddChild(surgeryButton);
            }
        }

        UpdateHeader();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_window == null || _guidance == null || !_window.IsOpen)
            return;

        _guidance.Reset();

        if (_part == null)
        {
            _guidance.ShowSelectPrompt();
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

        // A limb is chosen but no operation yet - name the one to start with, rather than leaving the
        // surgeon to work out which of six entries is relevant.
        if (!_entities.HasComponent<SurgeryComponent>(_surgery?.Ent))
        {
            _guidance.ShowChooseOperation(recommended, ActiveFocusName());
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

            // The glyph repeats what brightness says, so status survives colour-blindness.
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
                // First Next wins - a negative next.Step marks a whole run of them.
                nextButton ??= stepButton;
                stepButton.ToolTip = _system.CanPerformStepWithAvailable(user, Owner, _part.Value, stepButton.Step, out var popup)
                    ? null
                    : popup;
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(stepButton.Step)?.Icon?.Default;
            stepButton.Set(stepName, texture);
            i++;
        }

        _guidance.Show(nextButton, next != null, user, Owner, _part.Value);

        // Limb condition changes while the window is open, so the diagram tracks it here rather than
        // only when the selection changes.
        _dollPresenter?.Refresh(selectedNet);
    }

    // The step is performed as a do-after on the surgeon, so the bar belongs where they are looking
    // rather than floating over the patient.
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

    // A joined segmented control, matching the row this window used to have for its tabs.
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

    // Deliberately independent of what is in your hands: the point is to say "do this", not "you
    // could do this right now" - you often need to go and fetch the tool.
    //
    // A focus steers the recommendation but never hides an operation. Hiding would recreate the
    // problem where Mend Bones silently vanished and the surgeon had no idea it existed.
    private string? RefreshOperations(EntityUid user)
    {
        if (_window == null || _part == null || _classifier == null)
            return null;

        SurgeryOperationButton? recommended = null;
        var bestUrgency = int.MaxValue;

        var states = new List<(SurgeryOperationButton Op, bool Complete, bool Blocked, bool InFocus)>();

        foreach (var child in _window.Surgeries.Children)
        {
            if (child is not SurgeryOperationButton op)
                continue;

            var next = _system.GetNextStep(Owner, _part.Value, op.Surgery, user);
            var complete = next == null;
            var blocked = !complete && next!.Value.Surgery.Owner != op.Surgery;
            var inFocus = _classifier.Matches(op.Surgery, _focus);

            states.Add((op, complete, blocked, inFocus));

            if (complete || blocked || !inFocus)
                continue;

            var urgency = _classifier.UrgencyOf(op.Surgery);
            if (urgency >= bestUrgency)
                continue;

            bestUrgency = urgency;
            recommended = op;
        }

        foreach (var (op, complete, blocked, inFocus) in states)
        {
            string glyph;
            Color colour;

            if (complete)
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

    // Patient > Left Arm > [prerequisite chain] > Amputation. Every segment but the last navigates.
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
