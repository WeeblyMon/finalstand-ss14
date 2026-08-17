using System.Numerics;
using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.Placement;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._FinalStand.Placement;

// Ghost snaps to the nearest tile center. Validity comes from the shared placement rule.
public sealed class AlignFSPlacement : PlacementMode
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    private readonly SharedMapSystem _mapSystem;
    private readonly HandsSystem _handsSystem;
    private readonly SharedTransformSystem _transformSystem;
    private readonly FSPlacementRuleSystem _rule;

    private const float SearchBoxSize = 2f;
    private const float PlaceColorBaseAlpha = 0.5f;

    public AlignFSPlacement(PlacementManager pMan) : base(pMan)
    {
        IoCManager.InjectDependencies(this);
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _handsSystem = _entityManager.System<HandsSystem>();
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _rule = _entityManager.System<FSPlacementRuleSystem>();

        ValidPlaceColor = ValidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        var unalignedMouseCoords = ScreenToCursorGrid(mouseScreen);
        MouseCoords = unalignedMouseCoords.AlignWithClosestGridTile(SearchBoxSize, _entityManager);

        var gridId = _transformSystem.GetGrid(MouseCoords);

        if (!_entityManager.TryGetComponent<MapGridComponent>(gridId, out var mapGrid))
            return;

        CurrentTile = _mapSystem.GetTileRef(gridId.Value, mapGrid, MouseCoords);

        float tileSize = mapGrid.TileSize;
        GridDistancing = tileSize;

        MouseCoords = new EntityCoordinates(MouseCoords.EntityId, new Vector2(CurrentTile.X + tileSize / 2,
            CurrentTile.Y + tileSize / 2));
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return false;

        if (!_handsSystem.TryGetActiveItem(player, out var heldEntity))
            return false;

        if (!_entityManager.TryGetComponent<FSPlaceableComponent>(heldEntity, out var placeable))
            return false;

        return _rule.CanPlaceAt(player, position, placeable);
    }
}
