using Content.Shared.Movement.Systems;

namespace Content.Server._FinalStand.Leveling;

public sealed partial class FSSpeedBonusSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    // One step per level of a speed upgrade. A level already counted (the weapon rebought) adds nothing.
    public void Raise(EntityUid player, string upgradeId, int level, float perLevel)
    {
        var bonus = EnsureComp<FSSpeedBonusComponent>(player);
        if (bonus.UpgradeLevels.GetValueOrDefault(upgradeId) >= level)
            return;

        bonus.UpgradeLevels[upgradeId] = level;
        bonus.SpeedMultiplier += perLevel;
        _movement.RefreshMovementSpeedModifiers(player);
    }

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
