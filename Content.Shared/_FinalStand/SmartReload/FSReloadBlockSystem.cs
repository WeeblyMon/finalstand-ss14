using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._FinalStand.SmartReload;

public sealed class FSReloadBlockSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSReloadingComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(EntityUid gun, FSReloadingComponent comp, ref AttemptShootEvent args)
    {
        args.Cancelled = true;
        args.Message = "Reloading.";
    }
}
