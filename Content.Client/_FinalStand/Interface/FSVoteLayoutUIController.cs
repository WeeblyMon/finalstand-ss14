using Content.Client._FinalStand.WaveHud;
using Content.Client.UserInterface.Screens;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Interface;

public sealed partial class FSVoteLayoutUIController : UIController
{
    [Dependency] private IOverlayManager _overlays = default!;

    private const float Gap = 10f;
    private const float Spacing = 8f;

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var votes = UIManager.ActiveScreen switch
        {
            DefaultGameScreen game => game.VoteMenu,
            SeparatedChatGameScreen separated => separated.VoteMenu,
            _ => null,
        };
        if (votes == null || votes.ChildCount == 0)
            return;

        var margin = Gap;
        if (_overlays.TryGetOverlay<WaveHudOverlay>(out var hud) && hud.LeftColumnBottom > 0f)
        {
            var scale = votes.UIScale;
            var currentTop = votes.GlobalPixelPosition.Y - votes.Margin.Top * scale;
            margin = MathF.Max(Gap, (hud.LeftColumnBottom + Spacing - currentTop) / scale);
        }

        if (MathF.Abs(votes.Margin.Top - margin) > 0.5f)
            votes.Margin = new Thickness(votes.Margin.Left, margin, votes.Margin.Right, votes.Margin.Bottom);
    }
}
