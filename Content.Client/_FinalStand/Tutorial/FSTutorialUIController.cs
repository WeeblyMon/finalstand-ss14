using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.Gameplay;
using Content.Shared._FinalStand.Tutorial;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Tutorial;

public sealed class FSTutorialUIController : UIController, IOnStateExited<GameplayState>, IOnSystemChanged<FSUiEventRelaySystem>
{
    private static readonly TimeSpan ShowTime = TimeSpan.FromSeconds(25);
    private const float MaxFrameStep = 0.25f;

    private readonly Queue<FSTutorialTip> _queue = new();
    private PanelContainer? _card;
    private Label? _title;
    private RichTextLabel? _body;
    private float _remaining;

    public FSTutorialTip? Showing { get; private set; }

    public void OnSystemLoaded(FSUiEventRelaySystem system) => system.TutorialTip += Enqueue;

    public void OnSystemUnloaded(FSUiEventRelaySystem system) => system.TutorialTip -= Enqueue;

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
        if (_remaining <= 0f)
            Dismiss();
    }

    public void OnStateExited(GameplayState state) => Reset();

    public void Reset()
    {
        _queue.Clear();
        Showing = null;
        if (_card != null)
            _card.Visible = false;
    }

    public void Dismiss()
    {
        Showing = null;
        if (_card != null)
            _card.Visible = false;
        ShowNext();
    }

    private void ShowNext()
    {
        if (!_queue.TryDequeue(out var tip))
            return;

        EnsureCard();
        Showing = tip;
        _remaining = (float) ShowTime.TotalSeconds;
        _title!.Text = Loc.GetString(FSTutorialText.TitleLocId(tip));
        _body!.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color={FSUiPalette.TextPrimary.ToHex()}]{FormattedMessage.EscapeText(Loc.GetString(FSTutorialText.BodyLocId(tip)))}[/color]"));
        _card!.Visible = true;
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
        _body = new RichTextLabel { MaxWidth = 420 };
        var footer = new RichTextLabel { MaxWidth = 300, HorizontalExpand = true, VerticalAlignment = Control.VAlignment.Center };
        footer.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color={FSUiPalette.TextMuted.ToHex()}]{FormattedMessage.EscapeText(Loc.GetString("fs-tutorial-footer"))}[/color]"));
        var dismiss = new Button { Text = Loc.GetString("fs-tutorial-dismiss"), MinWidth = 80 };
        dismiss.OnPressed += _ => Dismiss();

        var footerRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        footerRow.AddChild(footer);
        footerRow.AddChild(dismiss);

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(14, 10),
        };
        content.AddChild(_title);
        content.AddChild(_body);
        content.AddChild(footerRow);

        _card = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = FSUiPalette.BgDeep.WithAlpha(0.97f),
                BorderColor = FSUiPalette.Currency.WithAlpha(0.6f),
                BorderThickness = new Thickness(1),
            },
            MaxWidth = 460,
            MouseFilter = Control.MouseFilterMode.Stop,
        };
        _card.AddChild(content);

        LayoutContainer.SetAnchorPreset(_card, LayoutContainer.LayoutPreset.CenterTop);
        LayoutContainer.SetGrowHorizontal(_card, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetMarginTop(_card, 90);
        UIManager.PopupRoot.AddChild(_card);
    }
}
