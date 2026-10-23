using Content.Shared.Inventory;
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
    private readonly SurgeryWindow _window;

    public SurgeryGuidancePresenter(IEntityManager entities, SurgeryWindow window)
    {
        _entities = entities;
        _window = window;
    }

    public void Reset()
    {
        _window.PerformButton.Disabled = true;
    }

    public void ShowSelectPrompt(EntityUid? user)
    {
        Set(null, WithSterility(Loc.GetString("surgery-ui-guidance-select"), user), NeutralColor);
    }

    public void ShowCannotOperate()
    {
        Set(null, Loc.GetString("surgery-ui-guidance-cannot-operate"), DangerColor);
    }

    public void ShowWorking()
    {
        Set(null, Loc.GetString("surgery-ui-guidance-working"), NeutralColor);
    }

    public void ShowStarting(EntityUid step)
    {
        Set(IconOf(step), Loc.GetString("surgery-ui-guidance-starting", ("step", NameOf(step))), NeutralColor);
    }

    public void ShowNext(EntityUid step, string details, EntityUid user)
    {
        _window.PerformButton.Disabled = false;

        var text = Loc.GetString("surgery-ui-guidance-next", ("step", NameOf(step)), ("details", details));
        Set(IconOf(step), WithSterility(text, user), IsSterile(user) ? ReadyColor : WarningColor);
    }

    public void ShowBlocked(EntityUid step, string reason)
    {
        Set(IconOf(step), Loc.GetString("surgery-ui-guidance-blocked", ("step", NameOf(step)), ("reason", reason)), WarningColor);
    }

    public void ShowWaiting(string problem, string reason)
    {
        Set(null, Loc.GetString("surgery-ui-guidance-waiting", ("problem", problem), ("reason", reason)), WarningColor);
    }

    public void ShowNothing(bool fixedSomething)
    {
        Set(null,
            Loc.GetString(fixedSomething ? "surgery-ui-guidance-all-fixed" : "surgery-ui-guidance-nothing-to-do"),
            ReadyColor);
    }

    private string WithSterility(string text, EntityUid? user)
    {
        if (user is not { } uid || IsSterile(uid))
            return text;

        return text + "\n" + Loc.GetString("surgery-ui-guidance-unsterile-note");
    }

    private string NameOf(EntityUid uid)
    {
        return _entities.GetComponent<MetaDataComponent>(uid).EntityName;
    }

    private Texture? IconOf(EntityUid step)
    {
        return _entities.GetComponentOrNull<SpriteComponent>(step)?.Icon?.Default;
    }

    private bool IsSterile(EntityUid user)
    {
        var inventory = _entities.System<InventorySystem>();

        return inventory.TryGetSlotEntity(user, "gloves", out _)
               && inventory.TryGetSlotEntity(user, "mask", out _);
    }

    private void Set(Texture? icon, string text, Color accent)
    {
        var msg = new FormattedMessage();
        msg.PushColor(accent);
        msg.AddText(text);
        msg.Pop();

        _window.GuidanceLabel.SetMessage(msg);
        _window.GuidanceIcon.Texture = icon;
        _window.GuidanceIcon.Visible = icon != null && _window.GuidanceVisible;

        _window.GuidancePanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = accent.WithAlpha(0.14f),
            BorderColor = accent.WithAlpha(0.55f),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
    }
}
