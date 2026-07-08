using Content.Client.Items;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client._FinalStand.Weapons;

public sealed class FSMinigunAmmoClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<FSMinigunComponent>(ent => new FSMinigunAmmoStatusControl(ent));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FSMinigunComponent, BallisticAmmoProviderComponent>();
        while (query.MoveNext(out _, out var minigun, out var ballistic))
        {
            minigun.CurrentAmmo = ballistic.Count;
            minigun.MaxAmmo = ballistic.Capacity;
        }
    }
}
