using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.WaveHud;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.WaveHud;

/// <summary>
///     Sends damage numbers only to the player who dealt the damage (no cross-player visual clutter).
///     Subscribes to CritLandedEvent (raised by CritSystem) so IsCrit is wired correctly.
/// </summary>
public sealed class FSDamageNumberServerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, CritLandedEvent>(OnCritLanded);
    }

    private void OnCritLanded(EntityUid uid, WaveSpawnedTagComponent _, CritLandedEvent args)
    {
        if (!TryComp<ActorComponent>(args.Shooter, out var actor))
            return;

        RaiseNetworkEvent(new FSDamageNumberEvent
        {
            Target = GetNetEntity(uid),
            Amount = args.FinalDamage,
            IsCrit = args.WasCrit,
        }, actor.PlayerSession);
    }
}
