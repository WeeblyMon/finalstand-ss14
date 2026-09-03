using Content.Shared._FinalStand.Mobs;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Mobs;

public sealed class FSGiantAbilityVisualizerSystem : VisualizerSystem<FSGiantAbilitiesComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, FSGiantAbilitiesComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<bool>(uid, FSGiantAbilityVisuals.Airborne, out var airborne, args.Component))
            return;

        _sprite.SetVisible((uid, args.Sprite), !airborne);
    }
}
