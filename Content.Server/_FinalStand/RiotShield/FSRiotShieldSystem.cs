using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.RiotShield;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.RiotShield;

public sealed class FSRiotShieldSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRiotShieldComponent, GotEquippedHandEvent>(OnShieldEquipped);
        SubscribeLocalEvent<FSRiotShieldComponent, GotUnequippedHandEvent>(OnShieldUnequipped);
        SubscribeLocalEvent<FSRiotShieldComponent, DamageChangedEvent>(OnShieldDamaged);
        SubscribeLocalEvent<FSRiotShieldUserComponent, DamageModifyEvent>(OnPlayerDamageModify);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnWavePrepStarted);
    }

    private void OnShieldEquipped(EntityUid uid, FSRiotShieldComponent comp, GotEquippedHandEvent args)
    {
        var marker = EnsureComp<FSRiotShieldUserComponent>(args.User);
        marker.Shield = uid;
    }

    private void OnShieldUnequipped(EntityUid uid, FSRiotShieldComponent comp, GotUnequippedHandEvent args)
    {
        if (TryComp<FSRiotShieldUserComponent>(args.User, out var marker) && marker.Shield == uid)
            RemComp<FSRiotShieldUserComponent>(args.User);
    }

    private void OnShieldDamaged(EntityUid uid, FSRiotShieldComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || comp.IsBroken)
            return;

        var blocked = (float) args.DamageDelta.GetTotal();
        comp.CurrentDurability -= blocked;
        if (comp.CurrentDurability <= 0f)
        {
            comp.CurrentDurability = 0f;
            comp.IsBroken = true;
        }
        Dirty(uid, comp);
    }

    private void OnPlayerDamageModify(EntityUid uid, FSRiotShieldUserComponent marker, DamageModifyEvent args)
    {
        if (args.OriginalDamage.GetTotal() <= FixedPoint2.Zero)
            return;
        if (!TryComp<FSRiotShieldComponent>(marker.Shield, out var shieldComp) || shieldComp.IsBroken)
            return;

        if (shieldComp.ThornsPercent > 0f && args.Origin is { } attacker)
            _damageable.TryChangeDamage(attacker, args.OriginalDamage * shieldComp.ThornsPercent, ignoreResistances: true);

        if (shieldComp.VampirePercent > 0f)
        {
            var healAmount = (float) args.OriginalDamage.GetTotal() * shieldComp.VampirePercent;
            _damageable.HealEvenly(uid, FixedPoint2.New(-healAmount));
            RaiseNetworkEvent(new FSHealNumberEvent
            {
                Target = GetNetEntity(uid),
                Amount = healAmount,
            }, Filter.Broadcast());
        }
    }

    private void OnWavePrepStarted(WavePrepStartedEvent _)
    {
        var query = EntityQueryEnumerator<FSRiotShieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsBroken)
                continue;

            _damageable.SetAllDamage(uid, FixedPoint2.Zero);
            comp.CurrentDurability = comp.BaseDurability * comp.DurabilityMultiplier;
            comp.IsBroken = false;
            Dirty(uid, comp);
        }
    }
}
