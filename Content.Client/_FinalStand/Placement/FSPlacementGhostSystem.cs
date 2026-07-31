using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.Placement;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._FinalStand.Placement;

// Mirrors RCDConstructionGhostSystem - shows a placement ghost while the held item is in FSPlaceableComponent.Placing mode.
public sealed class FSPlacementGhostSystem : EntitySystem
{
    private const string PlacementMode = nameof(AlignFSPlacement);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlacementManager _placementManager = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var placerEntity = _placementManager.CurrentPermission?.MobUid;
        var placerIsFsPlaceable = HasComp<FSPlaceableComponent>(placerEntity);

        if (_placementManager.Eraser || (placerEntity != null && !placerIsFsPlaceable))
            return;

        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return;

        var heldEntity = _hands.GetActiveItem(player);

        if (heldEntity != null && IsClientSide(heldEntity.Value))
            return;

        if (!TryComp<FSPlaceableComponent>(heldEntity, out var placeable) || !placeable.Placing)
        {
            if (placerIsFsPlaceable)
                _placementManager.Clear();

            return;
        }

        if (heldEntity == placerEntity)
            return;

        var newObjInfo = new PlacementInformation
        {
            MobUid = heldEntity.Value,
            PlacementOption = PlacementMode,
            EntityType = placeable.PreviewProtoId,
            Range = (int) MathF.Ceiling(placeable.Range),
            IsTile = false,
            UseEditorContext = false,
        };

        _placementManager.Clear();
        _placementManager.BeginPlacing(newObjInfo);
    }
}
