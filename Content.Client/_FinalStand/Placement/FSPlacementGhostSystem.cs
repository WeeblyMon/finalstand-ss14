using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Placement;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._FinalStand.Placement;

// Mirrors RCDConstructionGhostSystem - shows a placement ghost while the held item is in FSPlaceableComponent.Placing mode.
public sealed class FSPlacementGhostSystem : EntitySystem
{
    private const string PlacementMode = nameof(AlignFSPlacement);

    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPlacementManager _placementManager = default!;
    [Dependency] private HandsSystem _hands = default!;

    private Direction? _lastSentDirection;

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

            _lastSentDirection = null;
            return;
        }

        var directional = TryComp<FSDeployableItemComponent>(heldEntity, out var deployable)
                          && deployable.FaceDeployerDirection;
        var starting = heldEntity != placerEntity;

        if (directional && starting)
            _placementManager.Direction = Transform(player).LocalRotation.GetCardinalDir();

        if (directional && _placementManager.Direction != _lastSentDirection)
        {
            _lastSentDirection = _placementManager.Direction;
            RaiseNetworkEvent(new FSPlacementRotationMessage(GetNetEntity(heldEntity.Value), _placementManager.Direction));
        }

        if (!starting)
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
