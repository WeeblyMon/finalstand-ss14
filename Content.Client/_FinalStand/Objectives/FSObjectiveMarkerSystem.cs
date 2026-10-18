using Content.Shared._FinalStand.Objectives;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;

namespace Content.Client._FinalStand.Objectives;

public sealed partial class FSObjectiveMarkerSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, Color> _originalColors = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSObjectiveMarkerOverlay());
        SubscribeLocalEvent<FSObjectiveMarkerComponent, ComponentStartup>((uid, comp, _) => ApplyTint((uid, comp)));
        SubscribeLocalEvent<FSObjectiveMarkerComponent, AfterAutoHandleStateEvent>((uid, comp, _) => ApplyTint((uid, comp)));
        SubscribeLocalEvent<FSObjectiveMarkerComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSObjectiveMarkerOverlay>();
        _originalColors.Clear();
    }

    private void ApplyTint(Entity<FSObjectiveMarkerComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (ent.Comp.Tint is not { } tint)
        {
            RestoreColor((ent, sprite));
            return;
        }

        _originalColors.TryAdd(ent, sprite.Color);
        _sprite.SetColor((ent, sprite), tint);
    }

    private void OnShutdown(Entity<FSObjectiveMarkerComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            RestoreColor((ent, sprite));
        _originalColors.Remove(ent);
    }

    private void RestoreColor(Entity<SpriteComponent> ent)
    {
        if (_originalColors.Remove(ent, out var color))
            _sprite.SetColor(ent.AsNullable(), color);
    }
}
