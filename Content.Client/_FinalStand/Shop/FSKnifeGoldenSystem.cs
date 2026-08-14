using Content.Shared._FinalStand.Shop;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._FinalStand.Shop;

public sealed partial class FSKnifeGoldenSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
    }

    private void OnAfterHandleState(Entity<FSWeaponUpgradeStateComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (MetaData(ent).EntityPrototype?.ID != "SurvivalKnife")
            return;
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        _sprite.LayerSetRsiState((ent, sprite), 0, ent.Comp.KnifeGolden ? "icon-golden" : "icon");
    }
}
