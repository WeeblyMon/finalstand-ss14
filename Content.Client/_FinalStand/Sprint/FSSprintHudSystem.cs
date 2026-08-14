using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Shared._FinalStand.Sprint;
using Content.Shared.Damage.Components;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Sprint;

public sealed partial class FSSprintHudSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IBaseClient _client = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private Label? _staminaLabel;
    private Font? _font;

    private static readonly Color ColorHigh  = Color.FromHex("#69F0AE");
    private static readonly Color ColorMid   = Color.FromHex("#FFD740");
    private static readonly Color ColorLow   = Color.FromHex("#FF5252");
    private static readonly Color ColorEmpty = Color.FromHex("#B71C1C");

    public override void Initialize()
    {
        base.Initialize();
        _client.PlayerLeaveServer += OnLeft;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _client.PlayerLeaveServer -= OnLeft;
        RemoveLabel();
    }

    public override void FrameUpdate(float frameTime)
    {
        var current = _player.LocalSession?.AttachedEntity;

        if (current == null || !HasComp<FSSprintComponent>(current.Value))
        {
            RemoveLabel();
            return;
        }

        if (!TryComp<FSSprintComponent>(current.Value, out var sprint))
            return;

        float fraction;
        if (sprint.IsExhausted)
        {
            fraction = 0f;
        }
        else if (TryComp<StaminaComponent>(current.Value, out var stamina) && stamina.CritThreshold > 0f)
        {
            fraction = 1f - Math.Clamp(stamina.StaminaDamage / stamina.CritThreshold, 0f, 1f);
        }
        else
        {
            fraction = 1f;
        }

        var pct   = (int) MathF.Round(fraction * 100f);
        var text  = $"⚡ {pct}%";
        var color = fraction switch
        {
            >= 0.60f => ColorHigh,
            >= 0.30f => ColorMid,
            >  0.00f => ColorLow,
            _        => ColorEmpty,
        };

        var ui = _uiManager.GetActiveUIWidgetOrNull<AlertsUI>();
        if (ui == null)
        {
            _staminaLabel?.Orphan();
            _staminaLabel = null;
            return;
        }

        if (_staminaLabel == null)
        {
            _font ??= new VectorFont(
                _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf")), 16);

            _staminaLabel = new Label
            {
                HorizontalAlignment = Control.HAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0),
                FontOverride = _font,
            };
            ui.FSStatusContainer.AddChild(_staminaLabel);
        }

        _staminaLabel.Text = text;
        _staminaLabel.FontColorOverride = color;
    }

    private void OnLeft(object? _, PlayerEventArgs __)
    {
        RemoveLabel();
    }

    private void RemoveLabel()
    {
        _staminaLabel?.Orphan();
        _staminaLabel = null;
    }
}
