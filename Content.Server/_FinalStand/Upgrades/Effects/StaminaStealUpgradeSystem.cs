using Content.Server.Damage.Systems;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class StaminaStealUpgradeSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _stamina = default!;

    // Stamina drained from enemy per hit per level.
    private const float DrainPerLevel = 15f;
    // Stamina restored to the shooter per hit per level (fuels sprint).
    private const float RestorePerLevel = 10f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.StaminaStealLevel <= 0)
            return;

        // Drain stamina from target — they stagger briefly, but their high regen means it doesn't last.
        if (HasComp<StaminaComponent>(ev.Target))
            _stamina.TakeStaminaDamage(ev.Target, state.StaminaStealLevel * DrainPerLevel, source: ev.Shooter);

        // Restore stamina to the shooter so they can keep sprinting.
        if (HasComp<StaminaComponent>(ev.Shooter.Value))
            _stamina.TakeStaminaDamage(ev.Shooter.Value, -(state.StaminaStealLevel * RestorePerLevel));
    }
}
