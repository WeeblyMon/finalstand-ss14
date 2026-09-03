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
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body;
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

    private readonly SurgerySystem _system;
    [ViewVariables]
    private SurgeryWindow? _window;
    private EntityUid? _part;
    private bool _isBody;
    private (EntityUid Ent, EntProtoId Proto)? _surgery;
    private readonly List<EntProtoId> _previousSurgeries = new();

    // FINALSTAND: interaction state only. Patient condition owns hue elsewhere - the two must never
    // share a channel or "you can't do this" and "they are dying" become the same red.
    private static readonly Color ReadyColor = Color.FromHex("#3FA37A");
    private static readonly Color WarningColor = Color.FromHex("#C9A227");
    private static readonly Color DangerColor = Color.FromHex("#C0392B");
    private static readonly Color MutedColor = Color.FromHex("#7F8891");

    // Reconciliation keys. The steps key carries the part as well as the surgery, because step
    // buttons capture netPart in their closures - the same operation on a different limb must
    // rebuild or it would send the message to the old limb.
    private string? _partsKey;
    private string? _surgeriesKey;
    private (NetEntity Part, EntProtoId Surgery)? _stepsKey;

    // Doll slot brightness. Selection is brightness, never hue - see the colour note above.
    private static readonly Color DollAbsentColor = new(0.25f, 0.27f, 0.30f);
    private static readonly Color DollAvailableColor = new(0.85f, 0.85f, 0.85f);

    private static readonly Color StepCompleteColor = new(0.55f, 0.55f, 0.55f);
    private static readonly Color StepLockedColor = new(0.40f, 0.40f, 0.40f);

    private SurgeryStepButton? _nextStepButton;
    private SurgeryDollControl? _doll;
    private readonly Dictionary<TargetBodyPart, (NetEntity Net, List<EntProtoId> Surgeries)> _dollTargets = new();

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

            // One action, always in the same place, so the surgeon never hunts for the live step.
            _window.PerformButton.OnPressed += _ =>
            {
                if (_nextStepButton is { } next)
                    SendPredictedMessage(new SurgeryStepChosenBuiMsg(next.NetPart, next.SurgeryId, next.StepId, _isBody));
            };
        }

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

        // FINALSTAND: only rebuild the parts column when the set of parts actually changed. The old
        // code tore down all three columns on every server state update, which killed scroll
        // position and focus and made the window flicker mid-operation.
        // Keyed on the available operations too, not just the limbs - a completed step can make a
        // new operation available, and the part buttons capture their surgery list in a closure.
        var partsKey = string.Join(';',
            options.Select(o => $"{o.netEntity.Id}:{string.Join(',', state.Choices[o.netEntity])}"));
        if (partsKey != _partsKey)
        {
            _partsKey = partsKey;
            _window.Parts.DisposeAllChildren();
            _dollTargets.Clear();

            foreach (var (netEntity, _, partName, category) in options)
            {
                var surgeries = state.Choices[netEntity];

                // Limbs the doll can draw go on the doll. Everything else - the body itself, and any
                // anatomy the humanoid template does not cover - falls through to the list beside it,
                // so a non-humanoid patient is never misrepresented by a human diagram.
                if (OrganCategories.ToTarget(category) is { } target)
                {
                    _dollTargets[target] = (netEntity, surgeries);
                    continue;
                }

                var partButton = new ChoiceControl();

                partButton.Set(partName, null);
                partButton.Button.OnPressed += _ => OnPartPressed(netEntity, surgeries);

                _window.Parts.AddChild(partButton);
            }
        }

        // Re-apply the previous selection rather than dropping the player back to Parts.
        var restored = false;
        if (oldPart != null)
        {
            foreach (var (netEntity, entity, _, _) in options)
            {
                if (entity != oldPart)
                    continue;

                var surgeries = state.Choices[netEntity];
                if (oldSurgery is { } selected
                    && _system.GetSingleton(selected.Proto) is { } surgery
                    && _entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
                {
                    OnSurgeryPressed((surgery, surgeryComp), netEntity, selected.Proto);
                }
                else
                {
                    OnPartPressed(netEntity, surgeries);
                }

                restored = true;
                break;
            }
        }

        // The part is gone - amputated, most likely. Falling through to a stale selection would let
        // the player keep operating on something that is no longer attached.
        if (!restored)
        {
            _part = null;
            _isBody = false;
            _surgery = null;
            _previousSurgeries.Clear();
            View(ViewType.Parts);
            RefreshUI();
        }

        if (!_window.IsOpen)
            _window.OpenCentered();
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

        // FINALSTAND: the step list is static for a given part+surgery; only status changes, and
        // RefreshUI owns that. Rebuilding here on every tick is what caused the flicker.
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

        View(ViewType.Steps);
        RefreshUI();
    }

    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _isBody = _entities.HasComponent<BodyComponent>(_part);

        // FINALSTAND: rebuild only when the part or its available operations changed.
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
                var surgeryButton = new ChoiceControl();
                surgeryButton.Set(surgery.Name, null);

                surgeryButton.Button.OnPressed += _ => OnSurgeryPressed(surgery.Ent, netPart, surgery.Id);
                _window.Surgeries.AddChild(surgeryButton);
            }
        }

        RefreshUI();
        View(ViewType.Surgeries);
    }

    private void RefreshUI()
    {
        if (_window == null || !_window.IsOpen)
            return;

        // Cleared up front so every early return below leaves Perform correctly dead.
        _nextStepButton = null;
        _window.PerformButton.Disabled = true;

        // FINALSTAND: the guidance bar is always populated, including in the states the old code
        // early-returned from - a stale line is worse than no line.
        if (_part == null || !_entities.HasComponent<SurgeryComponent>(_surgery?.Ent))
        {
            SetGuidance(null, Loc.GetString("surgery-ui-guidance-select"));
            return;
        }

        if (!_entities.TryGetComponent(_player.LocalEntity, out SurgeryTargetComponent? surgeryComp)
            || !surgeryComp.CanOperate)
        {
            SetGuidance(null, Loc.GetString("surgery-ui-guidance-cannot-operate"), DangerColor);
            return;
        }

        var next = _system.GetNextStep(Owner, _part.Value, _surgery.Value.Ent, _player.LocalEntity.Value);
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

            // FINALSTAND: glyph carries the status so it survives colour-blindness and the greyout,
            // and the duration lets the surgeon judge whether there is time before committing.
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

            // FINALSTAND: brightness carries interaction state, so it can never be confused with the
            // patient's condition. The glyph above does the same job for colour-blind players.
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
                if (!_system.CanPerformStepWithHeld(_player.LocalEntity.Value, Owner, _part.Value, stepButton.Step, false, out var popup))
                    stepButton.ToolTip = popup;
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(stepButton.Step)?.Icon?.Default;
            stepButton.Set(stepName, texture);
            i++;
        }

        UpdateGuidance(nextButton, next != null);
    }

    // FINALSTAND: the whole point of the rewrite's first stage. CanPerformStepWithHeld already knows
    // exactly why a step will not work; the old UI spent that on a tooltip nobody hovers.
    private void UpdateGuidance(SurgeryStepButton? next, bool workRemains)
    {
        if (_window == null)
            return;

        _nextStepButton = next;

        if (next == null)
        {
            // No actionable step here but work remains means the next step lives in a prerequisite
            // surgery - saying "complete" there would be a lie.
            SetGuidance(null,
                workRemains
                    ? Loc.GetString("surgery-ui-guidance-prerequisite")
                    : Loc.GetString("surgery-ui-guidance-complete"),
                workRemains ? WarningColor : ReadyColor);
            return;
        }

        var stepName = _entities.GetComponent<MetaDataComponent>(next.Step).EntityName;
        var texture = _entities.GetComponentOrNull<SpriteComponent>(next.Step)?.Icon?.Default;

        if (_player.LocalEntity is not { } user || _part == null)
            return;

        if (_system.CanPerformStepWithHeld(user, Owner, _part.Value, next.Step, false, out var popup, out var reason))
        {
            _window.PerformButton.Disabled = false;
            SetGuidance(texture, Loc.GetString("surgery-ui-guidance-ready", ("step", stepName)), ReadyColor);
            return;
        }

        var detail = ReasonText(reason, next.Step, popup);
        SetGuidance(texture, Loc.GetString("surgery-ui-guidance-blocked", ("step", stepName), ("reason", detail)), WarningColor);
    }

    private string ReasonText(StepInvalidReason reason, EntityUid step, string? popup)
    {
        switch (reason)
        {
            case StepInvalidReason.MissingTool:
                var tools = _system.GetStepToolNames(step);
                if (tools.Count > 0)
                    return Loc.GetString("surgery-ui-guidance-need-tool", ("tool", string.Join(", ", tools)));
                break;
            case StepInvalidReason.NeedsOperatingTable:
                return Loc.GetString("surgery-ui-guidance-need-table");
            case StepInvalidReason.Armor:
                return Loc.GetString("surgery-ui-guidance-armor");
            case StepInvalidReason.MissingSkills:
                return Loc.GetString("surgery-ui-guidance-skills");
            case StepInvalidReason.MissingPreviousSteps:
                return Loc.GetString("surgery-ui-guidance-previous");
        }

        return popup ?? Loc.GetString("surgery-ui-guidance-blocked-generic");
    }

    // Built from AddText rather than markup so a bracket in an entity name cannot throw.
    private void SetGuidance(Texture? icon, string text, Color? accent = null)
    {
        if (_window == null)
            return;

        var msg = new FormattedMessage();
        if (accent is { } colour)
            msg.PushColor(colour);
        msg.AddText(text);
        if (accent != null)
            msg.Pop();

        _window.GuidanceLabel.SetMessage(msg);
        _window.GuidanceIcon.Texture = icon;
        _window.GuidanceIcon.Visible = icon != null;
    }

    // FINALSTAND: nothing is hidden any more, so this only maintains the header. Kept as View() so
    // every existing call site still reads naturally.
    private void View(ViewType type)
    {
        if (_window == null)
            return;

        BuildBreadcrumb();
        UpdateDoll();

        if (_entities.TryGetComponent(_part, out MetaDataComponent? partMeta) &&
            _entities.TryGetComponent(_surgery?.Ent, out MetaDataComponent? surgeryMeta))
            _window.Title = $"Surgery - {partMeta.EntityName}, {surgeryMeta.EntityName}";
        else if (partMeta != null)
            _window.Title = $"Surgery - {partMeta.EntityName}";
        else
            _window.Title = "Surgery";
    }

    // Handlers are wired once at construction and read _dollTargets on click, so rebuilding the part
    // set never has to churn event subscriptions.
    private SurgeryDollControl EnsureDoll()
    {
        if (_doll != null)
            return _doll;

        _doll = new SurgeryDollControl();

        foreach (var (target, button) in _doll.Slots)
        {
            var slot = target;
            button.OnPressed += _ =>
            {
                if (_dollTargets.TryGetValue(slot, out var entry))
                    OnPartPressed(entry.Net, entry.Surgeries);
            };
        }

        _window!.DollSlot.AddChild(_doll);
        return _doll;
    }

    private void UpdateDoll()
    {
        if (_window == null)
            return;

        // Nothing on this patient maps to the humanoid diagram - hide it rather than show a dead one.
        if (_dollTargets.Count == 0)
        {
            if (_doll != null)
                _doll.Visible = false;

            _window.BodyHeader.Visible = _window.Parts.ChildCount > 0;
            return;
        }

        var doll = EnsureDoll();
        doll.Visible = true;
        _window.BodyHeader.Visible = true;

        NetEntity? selected = _entities.TryGetNetEntity(_part, out var netPart) ? netPart : null;

        foreach (var (target, button) in doll.Slots)
        {
            if (!_dollTargets.TryGetValue(target, out var entry))
            {
                button.Disabled = true;
                button.Modulate = DollAbsentColor;
                continue;
            }

            button.Disabled = false;
            button.Modulate = selected == entry.Net ? Color.White : DollAvailableColor;
        }
    }

    // Patient ▸ Left Arm ▸ [prerequisite chain] ▸ Amputation. Every segment but the last navigates,
    // which is what the mislabelled Steps button was trying and failing to be.
    private void BuildBreadcrumb()
    {
        if (_window == null)
            return;

        _window.Breadcrumb.DisposeAllChildren();

        AddCrumb(Loc.GetString("surgery-ui-crumb-patient"), _part != null, () =>
        {
            _part = null;
            _isBody = false;
            _surgery = null;
            _previousSurgeries.Clear();
            View(ViewType.Parts);
            RefreshUI();
        });

        if (_part == null)
            return;

        var partName = _entities.GetComponent<MetaDataComponent>(_part.Value).EntityName;
        AddCrumb(partName, _surgery != null, () =>
        {
            _surgery = null;
            _previousSurgeries.Clear();

            if (!_entities.TryGetNetEntity(_part, out var netPart)
                || State is not SurgeryBuiState s
                || !s.Choices.TryGetValue(netPart.Value, out var surgeries))
                return;

            OnPartPressed(netPart.Value, surgeries);
        });

        // The prerequisite chain, oldest first. Clicking one truncates back to it.
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
                Text = " ▸ ",
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

    private enum ViewType
    {
        Parts,
        Surgeries,
        Steps
    }

    private enum StepStatus
    {
        Next,
        Complete,
        Incomplete
    }
}
