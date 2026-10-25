using Content.Client.Eui;
using Content.Shared._FinalStand.Admin;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Placement;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._FinalStand.Admin;

[UsedImplicitly]
public sealed partial class FSCustomZombieEui : BaseEui
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPlacementManager _placement = default!;

    private readonly FSCustomZombieWindow _window;

    public FSCustomZombieEui()
    {
        IoCManager.InjectDependencies(this);

        _window = new FSCustomZombieWindow();
        _window.OnClose += SendClosedMessage;
        _window.OnZombieSelected += RequestFields;
        _window.OnSpawnRequested += ArmPlacement;
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();

        if (_window.TryGetSelectedProto(out var protoId))
            RequestFields(protoId);
    }

    public override void Closed()
    {
        base.Closed();
        _window.OnClose -= SendClosedMessage;
        _window.OnZombieSelected -= RequestFields;
        _window.OnSpawnRequested -= ArmPlacement;
        _window.Close();
        ClearPlacement();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is FSCustomZombieEuiMsg.FieldData data)
            _window.LoadFields(data);
    }

    private void SendClosedMessage()
    {
        SendMessage(new CloseEuiMessage());
    }

    private void RequestFields(string protoId)
    {
        SendMessage(new FSCustomZombieEuiMsg.RequestFields { ProtoId = protoId });
    }

    private void ClearPlacement()
    {
        if (_placement.IsActive)
            _placement.Clear();
    }

    private void ArmPlacement(string protoId)
    {
        ClearPlacement();

        _placement.BeginPlacing(new PlacementInformation
        {
            IsTile = false,
            EntityType = protoId,
            PlacementOption = "SnapgridCenter",
        }, new FSCustomZombiePlacementHijack(this));

        _window.SetStatus($"Placing {protoId} — click a tile. Esc to stop.");
    }

    public void RequestSpawn(EntityCoordinates coordinates)
    {
        var msg = _window.BuildRequest();
        if (msg == null)
            return;

        msg.Coordinates = _entManager.GetNetCoordinates(coordinates);
        SendMessage(msg);
    }
}
