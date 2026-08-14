// Pirate cross-family crit: kill with cutlass arms flintlock; kill with flintlock arms cutlass. Consumed on use, dropped weapons untag, 5s expiry.
using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class FlintlockCritSynergyUpgradeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private CritSystem _crit = default!;

    private const float CritMultiplier = 1.5f;
    private static readonly TimeSpan ExpiryCleanupInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextCleanup;

    // prevents the kill from a crit-consuming swing from immediately re-arming the opposite weapon
    private readonly HashSet<EntityUid> _justConsumedCrit = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FSCritReadyComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnProjectileHitEffect);
        SubscribeLocalEvent<FSCritReadyComponent, GotUnequippedHandEvent>(OnArmedWeaponDropped);
    }

    public override void Update(float frameTime)
    {
        _justConsumedCrit.Clear();

        if (_timing.CurTime < _nextCleanup)
            return;
        _nextCleanup = _timing.CurTime + ExpiryCleanupInterval;

        var query = EntityQueryEnumerator<FSFlintlockCritWindowComponent>();
        while (query.MoveNext(out var uid, out var window))
        {
            if (window.ExpiresAt < _timing.CurTime)
                CloseWindow(uid);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Dead || ev.OldMobState == MobState.Dead)
            return;
        if (ev.Origin is not { } killer)
            return;
        if (!TryComp<HandsComponent>(killer, out var hands))
            return;

        if (_justConsumedCrit.Contains(killer))
        {
            return;
        }

        var anyCutlass = false;
        var anyFlintlock = false;
        foreach (var held in _hands.EnumerateHeld((killer, hands)))
        {
            if (IsFlintlock(held)) anyFlintlock = true;
            if (IsCutlassWithSynergy(held)) anyCutlass = true;
        }
        if (!anyCutlass && !anyFlintlock)
            return;

        if (!_hands.TryGetActiveItem((killer, hands), out var activeItem))
            return;

        var killedWithFlintlock = IsFlintlock(activeItem.Value);
        var killedWithCutlass = IsCutlassWithSynergy(activeItem.Value);
        if (!killedWithFlintlock && !killedWithCutlass)
            return;

        var durationSec = GetSynergyDuration(killer, hands);
        if (durationSec <= 0)
        {
            return;
        }

        var window = EnsureComp<FSFlintlockCritWindowComponent>(killer);
        var newExpiry = _timing.CurTime + TimeSpan.FromSeconds(durationSec);
        if (newExpiry > window.ExpiresAt)
            window.ExpiresAt = newExpiry;
        Dirty(killer, window);

        foreach (var held in _hands.EnumerateHeld((killer, hands)))
        {
            var armThis = killedWithCutlass ? IsFlintlock(held) : IsCutlassWithSynergy(held);
            if (armThis)
            {
                EnsureComp<FSCritReadyComponent>(held);
            }
        }
    }

    private int GetSynergyDuration(EntityUid wielder, HandsComponent hands)
    {
        var best = 0;
        foreach (var held in _hands.EnumerateHeld((wielder, hands)))
        {
            if (TryComp<FSWeaponUpgradeStateComponent>(held, out var s) && s.FlintlockCritDurationSec > best)
                best = s.FlintlockCritDurationSec;
        }
        return best;
    }

    private void OnMeleeHit(EntityUid weapon, FSCritReadyComponent _, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        args.BonusDamage += args.BaseDamage * (CritMultiplier - 1f);

        foreach (var target in args.HitEntities)
            _crit.MarkPendingCrit(args.User, target);

        _justConsumedCrit.Add(args.User);
        CloseWindow(args.User);
    }

    private void OnProjectileHitEffect(FSProjectileHitEffectEvent ev)
    {
        if (ev.Shooter is not { } shooter || ev.Weapon is not { } weapon)
            return;
        if (!HasComp<FSCritReadyComponent>(weapon))
            return;

        ev.AdditionalMultiplier *= CritMultiplier;
        _crit.MarkPendingCrit(shooter, ev.Target);

        _justConsumedCrit.Add(shooter);
        CloseWindow(shooter);
    }

    private void OnArmedWeaponDropped(EntityUid weapon, FSCritReadyComponent _, GotUnequippedHandEvent args)
    {
        RemComp<FSCritReadyComponent>(weapon);
    }

    private void CloseWindow(EntityUid wielder)
    {
        if (TryComp<HandsComponent>(wielder, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld((wielder, hands)))
                RemComp<FSCritReadyComponent>(held);
        }
        RemComp<FSFlintlockCritWindowComponent>(wielder);
    }

    // Exact ids: a substring match also caught FSCartridgeFlintlock and FSBulletFlintlock, so
    // holding flintlock ammo counted as holding the gun.
    private static readonly string[] FlintlockProtos =
    [
        "WeaponPistolFlintlock",
        "WeaponPistolFlintlockEmpty",
    ];

    private bool IsFlintlock(EntityUid weapon)
    {
        var protoId = MetaData(weapon).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        foreach (var candidate in FlintlockProtos)
        {
            if (protoId == candidate)
                return true;
        }
        return false;
    }

    private bool IsCutlassWithSynergy(EntityUid weapon)
    {
        return TryComp<FSWeaponUpgradeStateComponent>(weapon, out var state)
            && state.FlintlockCritDurationSec > 0;
    }
}
