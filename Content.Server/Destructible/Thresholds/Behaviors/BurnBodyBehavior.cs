using Content.Shared.Body.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BurnBodyBehavior : IThresholdBehavior
{
    [DataField]
    public LocId PopupMessage = "bodyburn-text-others";

    public void Execute(EntityUid bodyId, DestructibleSystem system, EntityUid? cause = null)
    {
        var mindSystem = system.EntityManager.System<SharedMindSystem>();
        if (mindSystem.TryGetMind(bodyId, out _, out var mindComp) && mindComp.UserId != null)
            return;

        var transformSystem = system.EntityManager.System<TransformSystem>();
        var inventorySystem = system.EntityManager.System<InventorySystem>();
        var sharedPopupSystem = system.EntityManager.System<SharedPopupSystem>();

        if (system.EntityManager.TryGetComponent<InventoryComponent>(bodyId, out var comp))
        {
            foreach (var item in inventorySystem.GetHandOrInventoryEntities(bodyId))
            {
                transformSystem.DropNextTo(item, bodyId);
            }
        }

        var bodyIdentity = Identity.Entity(bodyId, system.EntityManager);
        sharedPopupSystem.PopupCoordinates(Loc.GetString(PopupMessage, ("name", bodyIdentity)), transformSystem.GetMoverCoordinates(bodyId), PopupType.LargeCaution);

        system.EntityManager.QueueDeleteEntity(bodyId);
    }
}
