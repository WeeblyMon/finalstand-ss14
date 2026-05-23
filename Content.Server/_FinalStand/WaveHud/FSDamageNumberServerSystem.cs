using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.WaveHud;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.WaveHud;

public sealed class FSDamageNumberServerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, CritLandedEvent>(OnCritLanded);
        SubscribeLocalEvent<WaveSpawnedTagComponent, FSArmorAbsorbedEvent>(OnArmorAbsorbed);
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

    private void OnArmorAbsorbed(EntityUid uid, WaveSpawnedTagComponent _, FSArmorAbsorbedEvent args)
    {
        if (args.Shooter == null || args.Absorbed <= 0f)
            return;
        if (!TryComp<ActorComponent>(args.Shooter.Value, out var actor))
            return;

        RaiseNetworkEvent(new FSArmorDamageNumberEvent
        {
            Target = GetNetEntity(uid),
            Amount = args.Absorbed,
        }, actor.PlayerSession);
    }
}
