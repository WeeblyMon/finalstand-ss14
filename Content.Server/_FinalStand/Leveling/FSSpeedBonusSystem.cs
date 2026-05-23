using Content.Shared.Movement.Systems;

namespace Content.Server._FinalStand.Leveling;

public sealed class FSSpeedBonusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSpeedBonusComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(EntityUid uid, FSSpeedBonusComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(comp.SpeedMultiplier, comp.SpeedMultiplier);
    }
}
