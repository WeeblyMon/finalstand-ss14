using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Damage.Systems;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.WaveHud;

/// <summary>
///     Sends damage numbers only to the player who dealt the damage (no cross-player visual clutter).
/// </summary>
public sealed class FSDamageNumberServerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, WaveSpawnedTagComponent _, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased || args.Origin == null)
            return;

        var amount = args.DamageDelta.GetTotal().Float();
        if (amount <= 0f)
            return;

        // Only send to the player who dealt this damage — no cross-player visual clutter.
        if (!TryComp<ActorComponent>(args.Origin.Value, out var actor))
            return;

        RaiseNetworkEvent(new FSDamageNumberEvent
        {
            Target = GetNetEntity(uid),
            Amount = amount,
            IsCrit = false,
        }, actor.PlayerSession);
    }
}
