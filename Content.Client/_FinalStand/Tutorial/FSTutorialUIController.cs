using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.Gameplay;
using Content.Client._FinalStand.WaveHud;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Tutorial;

public sealed partial class FSTutorialUIController : UIController, IOnStateExited<GameplayState>, IOnSystemChanged<FSUiEventRelaySystem>
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly TimeSpan ShowTime = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan ReadyUpShowTime = TimeSpan.FromSeconds(40);
    private const float ArrowGap = 4f;
    private const float MaxFrameStep = 0.25f;
    private const float IconSize = 64f;
    private const float BodyWidth = 420f;

    private readonly Queue<FSTutorialTip> _queue = new();
    private PanelContainer? _card;
    private Label? _title;
    private RichTextLabel? _body;
    private TextureRect? _icon;
    private PanelContainer? _iconFrame;
    private FSTutorialArrow? _arrow;
    private float _remaining;

    public FSTutorialTip? Showing { get; private set; }

    public string? ShowingBody { get; private set; }

    public EntProtoId? ShowingIcon => Showing is { } tip ? FSTutorialText.Icon(tip) : null;

    public bool ShowingArrow => _arrow is { Visible: true };

    public void OnSystemLoaded(FSUiEventRelaySystem system) => system.TutorialTip += Enqueue;

    // Leaving a server takes the entity systems down with it, and the next queued card needs SpriteSystem.
    public void OnSystemUnloaded(FSUiEventRelaySystem system)
    {
        system.TutorialTip -= Enqueue;
        Reset();
    }

    public void Enqueue(FSTutorialTip tip)
    {
        if (Showing == tip || _queue.Contains(tip))
            return;

        _queue.Enqueue(tip);
        if (Showing == null)
            ShowNext();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Showing == null)
            return;

        _remaining -= Math.Min(args.DeltaSeconds, MaxFrameStep);
        if (_remaining <= 0f || Showing == FSTutorialTip.ReadyUp && !UpdateArrow())
            Dismiss();
    }

    public void OnStateExited(GameplayState state) => Reset();

    private bool UpdateArrow()
    {
        if (!_overlays.TryGetOverlay<WaveHudOverlay>(out var hud) || hud.ReadyUpPlayerIsReady)
            return false;

        var arrow = EnsureArrow();
        arrow.Visible = hud.IsReadyUpVisible && hud.ReadyUpYesBounds.Left > 0f;
        if (!arrow.Visible)
            return true;

        var scale = UIManager.PopupRoot.UIScale;
        var target = hud.ReadyUpYesBounds;
        LayoutContainer.SetPosition(arrow, new Vector2(
            target.Left / scale - arrow.SetSize.X - ArrowGap,
            (target.Top + target.Bottom) / 2f / scale - arrow.SetSize.Y / 2f));
        return true;
    }

    private FSTutorialArrow EnsureArrow()
    {
        if (_arrow is not { Disposed: false })
            _arrow = new FSTutorialArrow();
        if (_arrow.Parent == null)
            UIManager.PopupRoot.AddChild(_arrow);
        return _arrow;
    }

    public void Reset()
    {
        _queue.Clear();
        Showing = null;
        ShowingBody = null;
        Hide();
    }

    public void Dismiss()
    {
        Showing = null;
        ShowingBody = null;
        Hide();
        ShowNext();
    }

    private void Hide()
    {
        if (_card != null)
            _card.Visible = false;
        if (_arrow != null)
            _arrow.Visible = false;
    }

    private void ShowNext()
    {
        if (!_queue.TryDequeue(out var tip))
            return;

        EnsureCard();
        Showing = tip;
        _remaining = (float) (tip == FSTutorialTip.ReadyUp ? ReadyUpShowTime : ShowTime).TotalSeconds;
        _title!.Text = Loc.GetString(FSTutorialText.TitleLocId(tip));
        ShowingBody = Loc.GetString(FSTutorialText.BodyLocId(tip),
            ("reload", Key(ContentKeyFunctions.ReloadWeapon)),
            ("suit", Key(ContentKeyFunctions.SmartEquipSuitStorage)),
            ("grenade", Key(ContentKeyFunctions.QuickGrenade)),
            ("ping", Key(ContentKeyFunctions.FSPing)));
        _body!.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color={FSUiPalette.TextPrimary.ToHex()}]{ShowingBody}[/color]"));

        var icon = FSTutorialText.Icon(tip);
        _iconFrame!.Visible = icon != null;
        _icon!.Texture = icon is { } proto ? IconTexture(proto) : null;
        _card!.Visible = true;
    }

    private Texture IconTexture(EntProtoId proto)
    {
        if (_prototypes.TryIndex(proto, out var entity)
            && entity.TryGetComponent<FSEnemyIconComponent>(out var enemy, EntityManager.ComponentFactory)
            && !string.IsNullOrEmpty(enemy.IconPath)
            && _resources.TryGetResource<TextureResource>(new ResPath(enemy.IconPath), out var portrait))
            return portrait.Texture;

        return EntityManager.System<SpriteSystem>().Frame0(new SpriteSpecifier.EntityPrototype(proto));
    }

    private string Key(BoundKeyFunction function)
    {
        if (!_input.TryGetKeyBinding(function, out var binding))
            return Loc.GetString("fs-tutorial-unbound");

        var name = binding.GetKeyString();
        return FormattedMessage.EscapeText(string.IsNullOrWhiteSpace(name) ? binding.BaseKey.ToString() : name);
    }

    private void EnsureCard()
    {
        if (_card is { Disposed: false })
        {
            if (_card.Parent == null)
                UIManager.PopupRoot.AddChild(_card);
            return;
        }

        _title = new Label { FontColorOverride = FSUiPalette.Currency, StyleClasses = { "LabelHeading" } };
        _body = new RichTextLabel { MaxWidth = BodyWidth };
        var footer = new RichTextLabel { MaxWidth = 300, HorizontalExpand = true, VerticalAlignment = Control.VAlignment.Center };
        footer.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color={FSUiPalette.TextMuted.ToHex()}]{FormattedMessage.EscapeText(Loc.GetString("fs-tutorial-footer"))}[/color]"));
        var dismiss = new Button { Text = Loc.GetString("fs-tutorial-dismiss"), MinWidth = 80 };
        dismiss.OnPressed += _ => Dismiss();

        var footerRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        footerRow.AddChild(footer);
        footerRow.AddChild(dismiss);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        text.AddChild(_title);
        text.AddChild(_body);

        _icon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(IconSize, IconSize),
            TextureScale = new Vector2(2f, 2f),
            Margin = new Thickness(4),
        };
        _iconFrame = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = FSUiPalette.BgDeep.WithAlpha(0.6f),
                BorderColor = FSUiPalette.Currency.WithAlpha(0.25f),
                BorderThickness = new Thickness(1),
            },
            VerticalAlignment = Control.VAlignment.Top,
        };
        _iconFrame.AddChild(_icon);

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        row.AddChild(_iconFrame);
        row.AddChild(text);

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(14, 10),
        };
        content.AddChild(row);
        content.AddChild(footerRow);

        _card = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = FSUiPalette.BgDeep.WithAlpha(0.97f),
                BorderColor = FSUiPalette.Currency.WithAlpha(0.6f),
                BorderThickness = new Thickness(1),
            },
            MaxWidth = BodyWidth + IconSize + 60,
            MouseFilter = Control.MouseFilterMode.Stop,
        };
        _card.AddChild(content);

        LayoutContainer.SetAnchorPreset(_card, LayoutContainer.LayoutPreset.CenterTop);
        LayoutContainer.SetGrowHorizontal(_card, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetMarginTop(_card, 90);
        UIManager.PopupRoot.AddChild(_card);
    }
}
