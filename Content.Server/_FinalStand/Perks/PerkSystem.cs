using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Perks;

public sealed class PerkSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // Tracks guns that had FireRate boosted by DoubleTap so we can revert on perk removal.
    private readonly Dictionary<EntityUid, float> _doubleTapApplied = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PerkComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<PerkComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<PerkComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    // ---- Public API ----

    public bool AddPerk(EntityUid player, PerkType perk)
    {
        var comp = EnsureComp<PerkComponent>(player);
        if (comp.HasPerk(perk))
            return false;
        if (!comp.CanAddPerk())
            return false;

        comp.ActivePerks.Add(perk);
        Dirty(player, comp);

        ApplyPerkModifiers(player, perk);

        if (_playerManager.TryGetSessionByEntity(player, out var session))
            RaiseNetworkEvent(new PerkAddedEvent { Player = GetNetEntity(player), Perk = perk },
                Filter.SinglePlayer(session));

        return true;
    }

    public void RemoveAllPerks(EntityUid player)
    {
        if (!TryComp<PerkComponent>(player, out var comp) || comp.ActivePerks.Count == 0)
            return;

        comp.ActivePerks.Clear();
        Dirty(player, comp);

        RevertDoubleTap();
        _movement.RefreshMovementSpeedModifiers(player);

        if (_playerManager.TryGetSessionByEntity(player, out var session))
            RaiseNetworkEvent(new PerkRemovedAllEvent { Player = GetNetEntity(player) },
                Filter.SinglePlayer(session));
    }

    public float GetReloadMultiplier(EntityUid player)
    {
        if (TryComp<PerkComponent>(player, out var comp) && comp.HasPerk(PerkType.SpeedCola))
            return 0.5f;
        return 1.0f;
    }

    // ---- Stat application ----

    private void ApplyPerkModifiers(EntityUid player, PerkType perk)
    {
        switch (perk)
        {
            case PerkType.StaminUp:
                _movement.RefreshMovementSpeedModifiers(player);
                break;

            case PerkType.DoubleTap:
                ApplyDoubleTap(player);
                break;
        }
    }

    private void ApplyDoubleTap(EntityUid player)
    {
        if (!TryComp<HandsComponent>(player, out var hands))
            return;

        foreach (var handName in hands.SortedHands)
        {
            if (!_hands.TryGetHeldItem((player, hands), handName, out var held)
                || held == null
                || !TryComp<GunComponent>(held.Value, out var gun))
                continue;

            if (_doubleTapApplied.ContainsKey(held.Value))
                continue;

            _doubleTapApplied[held.Value] = gun.FireRate;

#pragma warning disable RA0002
            gun.FireRate /= 0.7f;
            gun.FireRateModified = gun.FireRate;
#pragma warning restore RA0002
            Dirty(held.Value, gun);
        }
    }

    private void RevertDoubleTap()
    {
        foreach (var (gunUid, origRate) in _doubleTapApplied)
        {
            if (!TryComp<GunComponent>(gunUid, out var gun))
                continue;

#pragma warning disable RA0002
            gun.FireRate = origRate;
            gun.FireRateModified = origRate;
#pragma warning restore RA0002
            Dirty(gunUid, gun);
        }
        _doubleTapApplied.Clear();
    }

    // ---- Event handlers ----

    // DamageModifyEvent.Damage is read back by DamageableSystem after the event —
    // this is the correct hook for percentage damage reduction (BeforeDamageChangedEvent.Damage is ignored).
    private void OnDamageModify(EntityUid uid, PerkComponent comp, DamageModifyEvent args)
    {
        if (!comp.HasPerk(PerkType.Juggernog))
            return;

        args.Damage *= 0.75f;
    }

    private void OnRefreshSpeed(EntityUid uid, PerkComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!comp.HasPerk(PerkType.StaminUp))
            return;

        args.ModifySpeed(1.2f);
    }

    private void OnMobStateChanged(EntityUid uid, PerkComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            RemoveAllPerks(uid);
    }
}
