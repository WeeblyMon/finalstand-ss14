using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Weapons;

public sealed partial class FSBulkLoaderSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBulkLoaderComponent, AfterInteractEvent>(OnAfterInteract, before: new[] { typeof(SharedGunSystem) });
        SubscribeLocalEvent<FSBulkLoaderComponent, FSBulkLoadDoAfterEvent>(OnBulkLoadDoAfter);
    }

    private void OnAfterInteract(Entity<FSBulkLoaderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || args.Used == args.Target || Deleted(args.Target))
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(ent, out var loader) || !loader.MayTransfer)
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(args.Target, out var target) || target.Whitelist == null)
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, loader.FillDelay, new FSBulkLoadDoAfterEvent(), used: ent, target: args.Target, eventTarget: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
        });
    }

    private void OnBulkLoadDoAfter(Entity<FSBulkLoaderComponent> ent, ref FSBulkLoadDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        args.Handled = true;
        BulkTransfer(ent, args.Target.Value, args.User);
    }

    public int BulkTransfer(EntityUid source, EntityUid target, EntityUid user)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(source, out var loader))
            return 0;

        if (Deleted(target) || !TryComp<BallisticAmmoProviderComponent>(target, out var targetComp) || targetComp.Whitelist == null)
            return 0;

        var loaderCount = loader.Entities.Count + loader.UnspawnedCount;
        var targetSpace = targetComp.Capacity - (targetComp.Entities.Count + targetComp.UnspawnedCount);
        var shots = Math.Min(loaderCount, targetSpace);

        if (shots <= 0)
            return 0;

        List<(EntityUid? Entity, IShootable Shootable)> ammo = new();
        var ev = new TakeAmmoEvent(shots, ammo, Transform(source).Coordinates, user);
        RaiseLocalEvent(source, ev);

        var inserted = 0;
        foreach (var (ammoEnt, _) in ammo)
        {
            if (ammoEnt == null)
                continue;

            if (_whitelist.IsWhitelistFail(targetComp.Whitelist, ammoEnt.Value))
            {
                // Doesn't fit this weapon after all — drop it back where the clip is instead of destroying it.
                _interaction.InteractUsing(user, ammoEnt.Value, source, Transform(source).Coordinates, checkCanInteract: false, checkCanUse: false);
                continue;
            }

            _interaction.InteractUsing(user, ammoEnt.Value, target, Transform(target).Coordinates, checkCanInteract: false, checkCanUse: false);
            inserted++;
        }

        if (inserted > 0)
            _audio.PlayPredicted(loader.SoundInsert, source, user);

        return inserted;
    }
}

[Serializable, NetSerializable]
public sealed partial class FSBulkLoadDoAfterEvent : SimpleDoAfterEvent
{
}
