using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.RiotShield;
using Content.Server.Popups;
using Content.Shared._FinalStand.Ammo;
using Content.Shared._FinalStand.Chainsaw;
using Content.Shared._FinalStand.RiotShield;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.Ammo;

public sealed partial class WaveAmmoBoxSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private FSRiotShieldSystem _riotShield = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveAmmoBoxComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<WaveAmmoBoxComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WaveAmmoBoxComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<WaveAmmoBoxComponent, WaveAmmoBoxRefillDoAfterEvent>(OnRefillDoAfter);
        SubscribeLocalEvent<WaveAmmoBoxComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnPowerChanged(EntityUid uid, WaveAmmoBoxComponent comp, ref PowerChangedEvent args)
    {
        comp.Enabled = args.Powered;
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var query = EntityQueryEnumerator<WaveAmmoBoxComponent>();
        while (query.MoveNext(out _, out var comp))
            comp.UsedBy.Clear();
    }

    private void OnActivate(EntityUid uid, WaveAmmoBoxComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = TryStartRefill(uid, comp, args.User);
    }

    private void OnInteractHand(EntityUid uid, WaveAmmoBoxComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = TryStartRefill(uid, comp, args.User);
    }

    private void OnInteractUsing(EntityUid uid, WaveAmmoBoxComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = TryStartRefill(uid, comp, args.User);
    }

    private bool TryStartRefill(EntityUid uid, WaveAmmoBoxComponent comp, EntityUid user)
    {
        if (!comp.Enabled)
        {
            _popup.PopupEntity(Loc.GetString("wave-ammo-box-unpowered"), uid, user);
            return false;
        }

        if (comp.UsedBy.Contains(user))
        {
            _popup.PopupEntity(Loc.GetString("wave-ammo-box-already-used"), uid, user);
            return false;
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, comp.RefillDuration,
            new WaveAmmoBoxRefillDoAfterEvent(), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnRefillDoAfter(EntityUid uid, WaveAmmoBoxComponent comp, WaveAmmoBoxRefillDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var user = args.Args.User;

        if (!comp.Enabled)
        {
            _popup.PopupEntity(Loc.GetString("wave-ammo-box-unpowered"), uid, user);
            return;
        }

        if (comp.UsedBy.Contains(user))
        {
            _popup.PopupEntity(Loc.GetString("wave-ammo-box-already-used"), uid, user);
            return;
        }

        comp.UsedBy.Add(user);
        RefillAllAmmo(user);
        _popup.PopupEntity(Loc.GetString("wave-ammo-box-used"), uid, user);
    }

    private void RefillAllAmmo(EntityUid player)
    {
        RefillContents(player, 0);
    }

    // A magazine can sit in a gun in a bag; the old two-level walk stopped one short of that.
    private void RefillContents(EntityUid holder, int depth)
    {
        const int maxDepth = 4;

        if (depth > maxDepth || !TryComp<ContainerManagerComponent>(holder, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                TryRefill(item);
                TryRepairShield(item);
                TryRefuelChainsaw(item);
                RefillContents(item, depth + 1);
            }
        }
    }

    private void TryRefill(EntityUid entity)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(entity, out var ballistic))
            return;
        var spawned = ballistic.Count - ballistic.UnspawnedCount;
        var target = ballistic.Capacity - spawned;
        if (target <= ballistic.UnspawnedCount)
            return;
        _gun.SetBallisticUnspawned((entity, ballistic), target);
    }

    private void TryRepairShield(EntityUid entity)
    {
        if (TryComp<FSRiotShieldComponent>(entity, out var shield))
            _riotShield.RepairShield(entity, shield);
    }

    private void TryRefuelChainsaw(EntityUid entity)
    {
        if (!TryComp<FSChainsawFuelComponent>(entity, out var fuel) || fuel.CurrentFuel >= fuel.MaxFuel)
            return;

        fuel.CurrentFuel = fuel.MaxFuel;
        Dirty(entity, fuel);
    }
}
