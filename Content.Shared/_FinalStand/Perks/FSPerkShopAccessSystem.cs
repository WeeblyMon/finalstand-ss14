using Content.Shared.Mobs.Components;

namespace Content.Shared._FinalStand.Perks;

public sealed partial class FSPerkShopAccessSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public const float UseRange = 2.5f;

    public bool CanUse(EntityUid user)
    {
        if (!HasComp<MobStateComponent>(user))
            return true;

        var userXform = Transform(user);
        var userPos = _transform.GetMapCoordinates(user, userXform);
        var query = EntityQueryEnumerator<FSPerkShopComponent, TransformComponent>();
        while (query.MoveNext(out var shop, out _, out var xform))
        {
            var shopPos = _transform.GetMapCoordinates(shop, xform);
            if (shopPos.MapId == userPos.MapId && (shopPos.Position - userPos.Position).Length() <= UseRange)
                return true;
        }

        return false;
    }
}
