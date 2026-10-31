using Content.Client._FinalStand.Interface;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.WaveHud;

public sealed class FSDownedPrompt : PanelContainer
{
    private static readonly Color ButtonHover = Color.InterpolateBetween(FSPalette.Money, FSPalette.TextBright, 0.3f);

    private readonly Label _offer;
    private readonly Label _status;
    private int _cost = -1;
    private string? _statusText;
    private bool _responded;

    public event Action? OnRevive;

    public FSDownedPrompt(IResourceCache cache)
    {
        var bold = cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf");

        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = FSPalette.PanelDeep.WithAlpha(0.95f),
            BorderColor = FSPalette.Danger,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 22,
            ContentMarginRightOverride = 22,
            ContentMarginTopOverride = 14,
            ContentMarginBottomOverride = 16,
        };
        MouseFilter = MouseFilterMode.Stop;

        var title = new Label
        {
            Text = Loc.GetString("fs-downed-title"),
            FontOverride = new VectorFont(bold, 28),
            FontColorOverride = FSPalette.HealthLow,
            HorizontalAlignment = HAlignment.Center,
        };

        _offer = new Label
        {
            FontOverride = new VectorFont(bold, 14),
            FontColorOverride = FSPalette.TextBright,
            HorizontalAlignment = HAlignment.Center,
        };

        _status = new Label
        {
            FontOverride = new VectorFont(bold, 14),
            HorizontalAlignment = HAlignment.Center,
        };

        var revive = new Button
        {
            Text = Loc.GetString("fs-downed-revive"),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
        };
        var normal = ButtonBox(FSPalette.Money);
        var hover = ButtonBox(ButtonHover);
        revive.StyleBoxOverride = normal;
        revive.Label.FontOverride = new VectorFont(bold, 13);
        revive.Label.ModulateSelfOverride = FSPalette.PanelDeep.WithAlpha(1f);
        revive.OnMouseEntered += _ => revive.StyleBoxOverride = hover;
        revive.OnMouseExited += _ => revive.StyleBoxOverride = normal;
        revive.OnPressed += _ => OnRevive?.Invoke();

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        column.AddChild(title);
        column.AddChild(_offer);
        column.AddChild(_status);
        column.AddChild(revive);
        AddChild(column);

        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.CenterTop);
        LayoutContainer.SetAnchorTop(this, 0.22f);
        LayoutContainer.SetAnchorBottom(this, 0.22f);
        LayoutContainer.SetGrowHorizontal(this, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetGrowVertical(this, LayoutContainer.GrowDirection.End);
    }

    public void Update(int cost, string? status, bool responded)
    {
        if (cost != _cost)
        {
            _cost = cost;
            _offer.Text = Loc.GetString("fs-downed-offer", ("cost", cost));
        }

        if (status == _statusText && responded == _responded)
            return;

        _statusText = status;
        _responded = responded;
        _status.Visible = status != null;
        _status.Text = status;
        _status.FontColorOverride = responded ? FSPalette.Ok : FSPalette.TextMuted;
    }

    private static StyleBoxFlat ButtonBox(Color fill)
    {
        var box = new StyleBoxFlat { BackgroundColor = fill };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 20);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        return box;
    }
}
