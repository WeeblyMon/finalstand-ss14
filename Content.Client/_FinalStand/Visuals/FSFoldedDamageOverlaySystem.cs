using Content.Client.Damage;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Visuals;

public sealed partial class FSFoldedDamageOverlaySystem : VisualizerSystem<FSFoldedDamageOverlayComponent>
{
    private const string OverlayLayer = "DamageOverlay";

    protected override void OnAppearanceChange(EntityUid uid, FSFoldedDamageOverlayComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !TryComp<DamageVisualsComponent>(uid, out var damageVisuals)
            || damageVisuals.DamageOverlay is not { } overlay
            || !AppearanceSystem.TryGetData<DoorState>(uid, DoorVisuals.State, out var state, args.Component))
            return;

        comp.Upright ??= overlay.Sprite;
        var folded = state is DoorState.Open or DoorState.Opening;
        var path = folded ? comp.Folded : comp.Upright;
        if (overlay.Sprite == path)
            return;

        overlay.Sprite = path;
        var sprite = (uid, args.Sprite);
        if (SpriteSystem.LayerMapTryGet(sprite, OverlayLayer, out var layer, false))
            SpriteSystem.LayerSetRsi(sprite, layer, new ResPath(path), SpriteSystem.LayerGetRsiState(sprite, layer));
    }
}
