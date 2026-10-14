// A thin brass line from each HMG to the crate feeding it, and for the gunner the arc the gun can traverse,
// edged red while the cursor is outside it.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.HMG;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._FinalStand.HMG;

public sealed partial class FSHMGLinkOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;

    private static readonly Color Belt = FSPalette.Money.WithAlpha(0.55f);
    private static readonly Color ArcEdge = FSPalette.TextBright.WithAlpha(0.45f);
    private static readonly Color ArcFill = FSPalette.TextBright.WithAlpha(0.05f);
    private static readonly Color ArcOut = FSPalette.Danger.WithAlpha(0.8f);
    private const float ArcRange = 6f;
    private const int ArcSteps = 24;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FSHMGLinkOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var xform = _entMan.System<SharedTransformSystem>();
        var query = _entMan.EntityQueryEnumerator<FSHMGComponent, TransformComponent>();
        while (query.MoveNext(out var hmg, out var gunXform))
        {
            if (gunXform.MapID == args.MapId
                && hmg.Gunner is { } gunner
                && gunner == _player.LocalEntity
                && _entMan.TryGetComponent<FSHMGGunnerComponent>(gunner, out var seat)
                && seat.ShowArc)
                DrawArc(args.WorldHandle, xform, hmg, gunXform);

            if (gunXform.MapID != args.MapId
                || hmg.Crate is not { } crate
                || !_entMan.TryGetComponent<TransformComponent>(crate, out var crateXform)
                || crateXform.MapID != args.MapId)
                continue;

            args.WorldHandle.DrawLine(xform.GetWorldPosition(gunXform), xform.GetWorldPosition(crateXform), Belt);
        }
    }

    private void DrawArc(DrawingHandleWorld handle, SharedTransformSystem xform, FSHMGComponent hmg, TransformComponent gunXform)
    {
        var origin = xform.GetWorldPosition(gunXform);
        var facing = xform.GetWorldRotation(gunXform) - Math.PI / 2;
        var half = Angle.FromDegrees((hmg.Arc + hmg.ArcBonus) / 2f);
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition).Position - origin;
        var outside = mouse.LengthSquared() > 0.01f && Math.Abs(Angle.ShortestDistance(facing, new Angle(mouse)).Degrees) > half.Degrees;

        var fan = new Vector2[ArcSteps + 2];
        fan[0] = origin;
        for (var i = 0; i <= ArcSteps; i++)
            fan[i + 1] = origin + (facing - half + half * 2 * i / ArcSteps).ToVec() * ArcRange;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, fan, ArcFill);
        var edge = outside ? ArcOut : ArcEdge;
        handle.DrawLine(origin, fan[1], edge);
        handle.DrawLine(origin, fan[^1], edge);
    }
}
