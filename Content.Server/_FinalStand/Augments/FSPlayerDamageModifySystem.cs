using Content.Shared._FinalStand.Augments;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Augments;

// Owns (ActorComponent, DamageModifyEvent) — handles Untouchable and FieldMedic.
public sealed class FSPlayerDamageModifySystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private static readonly TimeSpan ChargeReloadTime = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, DamageModifyEvent>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSUntouchableComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var unt, out _))
        {
            if (!_mind.TryGetMind(uid, out var mindId, out _)) continue;
            if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) continue;
            var maxCharges = augs.GetSlottedLevel("Untouchable");
            if (maxCharges <= 0)
            {
                RemComp<FSUntouchableComponent>(uid);
                continue;
            }

            if (unt.CurrentCharges < maxCharges && now >= unt.NextChargeTime && unt.NextChargeTime != default)
            {
                unt.CurrentCharges++;
                unt.NextChargeTime = unt.CurrentCharges < maxCharges ? now + ChargeReloadTime : default;
                SendChargesUpdate(uid, unt.CurrentCharges);
            }
        }
    }

    private void OnDamageModify(EntityUid uid, ActorComponent comp, DamageModifyEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out MindComponent? _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        // Untouchable: block one hit per charge, negate all incoming damage.
        var untouchableLevel = augs.GetSlottedLevel("Untouchable");
        if (untouchableLevel > 0 && args.Damage.GetTotal().Float() > 0)
        {
            var unt = EnsureComp<FSUntouchableComponent>(uid);
            // Pre-load charges on first creation (CurrentCharges==0 with no timer means brand-new component).
            if (unt.CurrentCharges == 0 && unt.NextChargeTime == default)
            {
                unt.CurrentCharges = untouchableLevel;
                SendChargesUpdate(uid, unt.CurrentCharges);
            }
            if (unt.CurrentCharges > 0)
            {
                args.Damage = new DamageSpecifier();
                unt.CurrentCharges--;
                if (unt.NextChargeTime == default)
                    unt.NextChargeTime = _timing.CurTime + ChargeReloadTime;
                SendChargesUpdate(uid, unt.CurrentCharges);
                return;
            }
        }

        // Field Medic: boost incoming healing (negative damage).
        var medicLevel = augs.GetSlottedLevel("FieldMedic");
        if (medicLevel > 0 && args.Damage.GetTotal().Float() < 0)
        {
            args.Damage *= 1f + medicLevel * 0.15f;
        }
    }

    private void SendChargesUpdate(EntityUid bodyUid, int charges)
    {
        if (!TryComp<ActorComponent>(bodyUid, out var actor)) return;
        RaiseNetworkEvent(new FSAugmentStacksUpdateEvent { AugId = "Untouchable", Stacks = charges },
            Filter.SinglePlayer(actor.PlayerSession));
    }
}
