// A thin brass line from each HMG to the crate feeding it, so a glance shows which crate feeds which gun.
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.HMG;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._FinalStand.HMG;

public sealed class FSHMGLinkOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;

    private static readonly Color Belt = FSPalette.Money.WithAlpha(0.55f);

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
            if (gunXform.MapID != args.MapId
                || hmg.Crate is not { } crate
                || !_entMan.TryGetComponent<TransformComponent>(crate, out var crateXform)
                || crateXform.MapID != args.MapId)
                continue;

            args.WorldHandle.DrawLine(xform.GetWorldPosition(gunXform), xform.GetWorldPosition(crateXform), Belt);
        }
    }
}
