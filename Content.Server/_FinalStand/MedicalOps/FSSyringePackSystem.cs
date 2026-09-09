using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSSyringePackSystem : EntitySystem
{
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly List<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSyringePackComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<FSSyringePackComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        if (!TryComp<StorageComponent>(target, out var gunStorage))
            return;

        if (!_containers.TryGetContainer(ent.Owner, ent.Comp.Container, out var packContainer)
            || packContainer.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-syringe-pack-empty"), ent, args.User);
            args.Handled = true;
            return;
        }

        _pending.Clear();
        _pending.AddRange(packContainer.ContainedEntities);

        var moved = 0;
        foreach (var syringe in _pending)
        {
            if (!_storage.Insert(target, syringe, out _, user: args.User, storageComp: gunStorage, playSound: moved == 0))
                break;

            moved++;
        }

        args.Handled = true;

        _popup.PopupEntity(
            moved > 0
                ? Loc.GetString("fs-syringe-pack-loaded", ("count", moved))
                : Loc.GetString("fs-syringe-pack-full"),
            ent,
            args.User);
    }
}
