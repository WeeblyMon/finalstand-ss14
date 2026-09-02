using Content.Shared._FinalStand.Deployables;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSSentryTurretVisualizerSystem : VisualizerSystem<FSSentryTurretComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, FSSentryTurretComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);

        if (AppearanceSystem.TryGetData<double>(uid, FSSentryTurretVisuals.Angle, out var theta, args.Component))
            _sprite.LayerSetRotation(sprite, FSSentryTurretLayers.Gun, new Angle(theta));

        if (AppearanceSystem.TryGetData<bool>(uid, FSSentryTurretVisuals.Firing, out var firing, args.Component))
            _sprite.LayerSetRsiState(sprite, FSSentryTurretLayers.Gun, firing ? "fire" : "turret");
    }
}
