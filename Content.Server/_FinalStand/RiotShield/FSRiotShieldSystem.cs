using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.RiotShield;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Blocking;
using Content.Shared.Blocking.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.RiotShield;

public sealed partial class FSRiotShieldSystem : EntitySystem
{
    [Dependency] private BlockingSystem _blocking = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

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
        comp.Wielder = args.User;
    }

    private void OnShieldUnequipped(EntityUid uid, FSRiotShieldComponent comp, GotUnequippedHandEvent args)
    {
        if (TryComp<FSRiotShieldUserComponent>(args.User, out var marker) && marker.Shield == uid)
            RemComp<FSRiotShieldUserComponent>(args.User);
        comp.Wielder = null;
    }

    private void OnShieldDamaged(EntityUid uid, FSRiotShieldComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || comp.IsBroken)
            return;

        if (comp.Wielder is { } wielder && _mobState.IsIncapacitated(wielder))
            return;

        var blocked = (float) args.DamageDelta.GetTotal();
        comp.CurrentDurability -= blocked;
        if (comp.CurrentDurability <= 0f)
        {
            comp.CurrentDurability = 0f;
            comp.IsBroken = true;

            if (TryComp<BlockingComponent>(uid, out var blocking) && blocking.User is { } user)
                _blocking.LowerShield((uid, blocking), user);
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
            }, Filter.Entities(uid));
        }
    }

    private void OnWavePrepStarted(WavePrepStartedEvent _)
    {
        var query = EntityQueryEnumerator<FSRiotShieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
            RepairShield(uid, comp);
    }

    public void RepairShield(EntityUid uid, FSRiotShieldComponent comp)
    {
        var maxDurability = comp.BaseDurability * comp.DurabilityMultiplier;
        if (!comp.IsBroken && comp.CurrentDurability >= maxDurability)
            return;

        _damageable.SetAllDamage(uid, FixedPoint2.Zero);
        comp.CurrentDurability = maxDurability;
        comp.IsBroken = false;
        Dirty(uid, comp);
    }
}
