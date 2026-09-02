using System.Numerics;
using Content.Shared._FinalStand.Weapons.Visuals;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Weapons;

// scales a charged pellet to match how long its shot was held
public sealed class FSChargeShotVisualizerSystem : VisualizerSystem<AppearanceComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, AppearanceComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<float>(uid, FSChargeShotVisuals.PelletScale, out var scale, component))
            return;

        _sprite.SetScale((uid, args.Sprite), new Vector2(scale, scale));
    }
}
