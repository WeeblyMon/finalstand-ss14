using Content.Shared.Destructible;
using Content.Shared._FinalStand.Deployables;
using Content.Shared.Mind;
using Robust.Server.Player;

namespace Content.Server._FinalStand.Deployables;

public sealed partial class FSDeployableAlertSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSentryTurretComponent, DestructionEventArgs>(OnDestroyed);
    }

    public void AmmoSpent(EntityUid uid, FSSentryTurretComponent turret)
    {
        var threshold = FSSentryAmmo.LowThreshold(turret);
        if (turret.Ammo <= 0)
            Send(uid, FSDeployableAlert.Spent);
        else if (turret.Ammo == threshold - 1)
            Send(uid, FSDeployableAlert.LowAmmo);
    }

    private void OnDestroyed(Entity<FSSentryTurretComponent> ent, ref DestructionEventArgs args)
    {
        Send(ent, FSDeployableAlert.Lost);
    }

    private void Send(EntityUid uid, FSDeployableAlert alert)
    {
        if (!TryComp<FSDeployedByComponent>(uid, out var deployed)
            || !TryComp<MindComponent>(deployed.OwnerMind, out var mind)
            || mind.UserId is not { } userId
            || !_players.TryGetSessionById(userId, out var session))
            return;

        RaiseNetworkEvent(new FSDeployableAlertEvent(GetNetEntity(uid), GetNetCoordinates(Transform(uid).Coordinates), alert, Name(uid)),
            session);
    }
}
