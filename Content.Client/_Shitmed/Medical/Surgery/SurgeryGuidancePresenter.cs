// FINALSTAND: owns the surgery guidance bar.

using Content.Shared._Shitmed.Medical.Surgery;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Shitmed.Medical.Surgery;

public sealed class SurgeryGuidancePresenter
{
    private static readonly Color ReadyColor = Color.FromHex("#3FA37A");
    private static readonly Color WarningColor = Color.FromHex("#C9A227");
    private static readonly Color DangerColor = Color.FromHex("#C0392B");
    private static readonly Color NeutralColor = Color.FromHex("#9BA3AE");

    private readonly IEntityManager _entities;
    private readonly SurgerySystem _system;
    private readonly SurgeryWindow _window;

    public SurgeryGuidancePresenter(IEntityManager entities, SurgerySystem system, SurgeryWindow window)
    {
        _entities = entities;
        _system = system;
        _window = window;
    }

    public SurgeryStepButton? NextStep { get; private set; }

    public void Reset()
    {
        NextStep = null;
        _window.PerformButton.Disabled = true;
    }

    public void ShowSelectPrompt()
    {
        Set(null, Loc.GetString("surgery-ui-guidance-select"));
    }

    public void ShowCannotOperate()
    {
        Set(null, Loc.GetString("surgery-ui-guidance-cannot-operate"), DangerColor);
    }

    public void ShowChooseOperation(string? recommended, string? focusFilter = null)
    {
        if (recommended != null)
        {
            Set(null, Loc.GetString("surgery-ui-guidance-start-with", ("operation", recommended)), ReadyColor);
            return;
        }

        if (focusFilter != null)
        {
            Set(null, Loc.GetString("surgery-ui-guidance-nothing-in-focus", ("focus", focusFilter)), WarningColor);
            return;
        }

        Set(null, Loc.GetString("surgery-ui-guidance-nothing-to-do"), ReadyColor);
    }

    public void Show(SurgeryStepButton? next, bool workRemains, EntityUid user, EntityUid body, EntityUid part)
    {
        NextStep = next;

        if (next == null)
        {
            Set(null,
                workRemains
                    ? Loc.GetString("surgery-ui-guidance-prerequisite")
                    : Loc.GetString("surgery-ui-guidance-complete"),
                workRemains ? WarningColor : ReadyColor);
            return;
        }

        var stepName = _entities.GetComponent<MetaDataComponent>(next.Step).EntityName;
        var texture = _entities.GetComponentOrNull<SpriteComponent>(next.Step)?.Icon?.Default;

        if (_system.CanPerformStepWithAvailable(user, body, part, next.Step, out var popup, out var reason))
        {
            _window.PerformButton.Disabled = false;
            Set(texture, Loc.GetString("surgery-ui-guidance-ready", ("step", stepName)), ReadyColor);
            return;
        }

        var detail = ReasonText(reason, next.Step, popup);
        Set(texture, Loc.GetString("surgery-ui-guidance-blocked", ("step", stepName), ("reason", detail)), WarningColor);
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

    private void Set(Texture? icon, string text, Color? accent = null)
    {
        var colour = accent ?? NeutralColor;

        var msg = new FormattedMessage();
        msg.PushColor(colour);
        msg.AddText(text);
        msg.Pop();

        _window.GuidanceLabel.SetMessage(msg);
        _window.GuidanceIcon.Texture = icon;
        _window.GuidanceIcon.Visible = icon != null;

        _window.GuidancePanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = colour.WithAlpha(0.14f),
            BorderColor = colour.WithAlpha(0.55f),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
    }
}
