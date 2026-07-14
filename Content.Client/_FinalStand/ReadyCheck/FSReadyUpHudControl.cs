using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._FinalStand.ReadyCheck;

public sealed class FSReadyUpHudControl : BoxContainer
{
    public event Action<bool>? OnReadyPressed;

    private readonly Label _countLabel;
    private readonly ClickablePanel _yesPanel;
    private readonly ClickablePanel _noPanel;

    private static readonly Color Muted  = Color.FromHex("#8FA1B3");
    private static readonly Color SepCol = new(0.23f, 0.26f, 0.32f, 0.8f);

    public FSReadyUpHudControl()
    {
        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Pass;

        AddChild(new PanelContainer
        {
            SetHeight = 1,
            PanelOverride = new StyleBoxFlat { BackgroundColor = SepCol },
        });

        var inner = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(0, 4, 0, 4),
        };
        AddChild(inner);

        inner.AddChild(new Label { Text = "READY UP", Modulate = Muted });

        _countLabel = new Label
        {
            Text = "—",
            Modulate = Color.White,
            Margin = new Thickness(0, 1, 0, 3),
        };
        inner.AddChild(_countLabel);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        inner.AddChild(row);

        _yesPanel = new ClickablePanel(
            Color.FromHex("#1a3d1a"),
            "YES", Color.FromHex("#44CC44"),
            () => OnReadyPressed?.Invoke(true));
        _yesPanel.HorizontalExpand = true;

        _noPanel = new ClickablePanel(
            Color.FromHex("#3d1a1a"),
            "NO", Color.FromHex("#CC4444"),
            () => OnReadyPressed?.Invoke(false));
        _noPanel.HorizontalExpand = true;

        row.AddChild(_yesPanel);
        row.AddChild(new Control { SetWidth = 3 });
        row.AddChild(_noPanel);
    }

    public void UpdateState(int readyCount, int totalCount, bool playerIsReady)
    {
        _countLabel.Text = totalCount > 0
            ? $"{readyCount} / {totalCount} ready"
            : "—";
        _countLabel.Modulate = readyCount > 0 ? Color.FromHex("#44FF44") : Color.White;

        _yesPanel.SetBackground(playerIsReady ? Color.FromHex("#2a6b2a") : Color.FromHex("#1a3d1a"));
        _noPanel.SetBackground(!playerIsReady && totalCount > 0 ? Color.FromHex("#6b2a2a") : Color.FromHex("#3d1a1a"));
    }
}

internal sealed class ClickablePanel : PanelContainer
{
    private readonly Action _onClick;

    public ClickablePanel(Color bgColor, string text, Color textColor, Action onClick)
    {
        _onClick = onClick;
        MouseFilter = MouseFilterMode.Stop;
        PanelOverride = new StyleBoxFlat { BackgroundColor = bgColor };
        AddChild(new Label
        {
            Text = text,
            HorizontalAlignment = HAlignment.Center,
            Modulate = textColor,
            Margin = new Thickness(2, 2, 2, 2),
        });
    }

    public void SetBackground(Color color)
    {
        PanelOverride = new StyleBoxFlat { BackgroundColor = color };
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _onClick();
            args.Handle();
        }
    }
}
