using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.WaveHud;

public sealed class FSDamageNumberServerSystem : EntitySystem
{
    public bool PlayerDamageNumbersEnabled { get; private set; }

    public void TogglePlayerDamageNumbers(IConsoleShell? shell = null)
    {
        PlayerDamageNumbersEnabled = !PlayerDamageNumbersEnabled;
        shell?.WriteLine($"Player damage numbers: {(PlayerDamageNumbersEnabled ? "ON" : "OFF")}");
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, CritLandedEvent>(OnCritLanded);
        SubscribeLocalEvent<WaveSpawnedTagComponent, FSArmorAbsorbedEvent>(OnArmorAbsorbed);
        SubscribeLocalEvent<ActorComponent, DamageChangedEvent>(OnPlayerDamaged);
    }

    private void OnPlayerDamaged(EntityUid uid, ActorComponent actor, DamageChangedEvent args)
    {
        if (!PlayerDamageNumbersEnabled) return;
        if (!args.DamageIncreased || args.DamageDelta == null) return;
        var total = 0f;
        foreach (var amount in args.DamageDelta.DamageDict.Values)
        {
            if (amount > 0)
                total += amount.Float();
        }
        if (total <= 0f) return;
        RaiseNetworkEvent(new FSDamageNumberEvent
        {
            Target = GetNetEntity(uid),
            Amount = total,
            IsCrit = false,
        }, actor.PlayerSession);
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
