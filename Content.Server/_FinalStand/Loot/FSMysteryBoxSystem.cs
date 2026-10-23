using System.Numerics;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Effects;
using Content.Server._FinalStand.Shop;
using Content.Shared._FinalStand.Loot;
using Content.Shared._FinalStand.Particles;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Loot;

public sealed partial class FSMysteryBoxSystem : EntitySystem
{
    [Dependency] private FSFlairSystem _flair = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSShopWeaponSystem _shop = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> OpenBurst = "FSUpgradeGlints";
    private static readonly ProtoId<FSParticleEmitterPrototype> LandBurst = "FSUpgradeRing";
    private static readonly Color BurstTint = Color.FromHex("#9FD8FF");
    private static readonly Vector2 BoxCenter = new(0.5f, 0.2f);

    private Dictionary<string, FSShopWeaponComponent>? _shops;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMysteryBoxComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<FSMysteryBoxComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _shops = null);
    }

    private void OnInteractHand(Entity<FSMysteryBoxComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = Use(ent, args.User);
    }

    private void OnActivate(Entity<FSMysteryBoxComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = Use(ent, args.User);
    }

    private bool Use(Entity<FSMysteryBoxComponent> ent, EntityUid user)
    {
        switch (ent.Comp.State)
        {
            case FSMysteryBoxState.Idle:
                return TrySpin(ent, user);
            case FSMysteryBoxState.Offering when ent.Comp.Buyer == user:
                Take(ent, user);
                return true;
            case FSMysteryBoxState.Offering:
                _popup.PopupEntity(Loc.GetString("fs-mystery-box-not-yours"), ent, user);
                return false;
            default:
                _popup.PopupEntity(Loc.GetString("fs-mystery-box-busy"), ent, user);
                return false;
        }
    }

    private bool TrySpin(Entity<FSMysteryBoxComponent> ent, EntityUid user)
    {
        if (!TryRoll(ent.Comp, out var prize))
        {
            Log.Error($"{ToPrettyString(ent)} has no resolvable loot pool; refusing to charge {ToPrettyString(user)}.");
            return false;
        }

        if (!_mind.TryGetMind(user, out var mindId, out _) || !_wallet.TryDeductCredits(mindId, ent.Comp.Price))
        {
            _popup.PopupEntity(Loc.GetString("fs-mystery-box-no-funds", ("price", ent.Comp.Price)), ent, user);
            return false;
        }

        ent.Comp.Buyer = user;
        ent.Comp.PendingPrize = prize;
        SetState(ent, FSMysteryBoxState.Spinning, ent.Comp.SpinTime);
        _audio.PlayPvs(ent.Comp.OpenSound, ent);
        _audio.PlayPvs(ent.Comp.SpinSound, ent);
        _flair.Burst(OpenBurst, Transform(ent).Coordinates.Offset(BoxCenter), BurstTint);
        return true;
    }

    private bool TryRoll(FSMysteryBoxComponent comp, out EntProtoId prize)
    {
        prize = default;

        var weights = new Dictionary<FSMysteryBoxTier, float>();
        foreach (var tier in comp.Tiers)
        {
            if (_protoMan.TryIndex(tier.Pool, out var pool) && pool.Weights.Count > 0)
                weights[tier] = tier.Weight;
        }

        if (weights.Count == 0)
            return false;

        prize = _random.Pick(_protoMan.Index(_random.Pick(weights).Pool).Weights);
        return true;
    }

    private void Take(Entity<FSMysteryBoxComponent> ent, EntityUid user)
    {
        if (ent.Comp.Prize is { } prize)
        {
            if (GetShops().TryGetValue(prize, out var shop))
                _shop.GrantWeapon(user, shop);
            else
                _shop.TryGiveItemToPlayer(user, Spawn(prize, Transform(user).Coordinates));
        }

        Close(ent);
    }

    private void Close(Entity<FSMysteryBoxComponent> ent)
    {
        ent.Comp.Buyer = null;
        ent.Comp.PendingPrize = null;
        ent.Comp.Prize = null;
        SetState(ent, FSMysteryBoxState.Closing, ent.Comp.CloseTime);
        _audio.PlayPvs(ent.Comp.CloseSound, ent);
    }

    private void SetState(Entity<FSMysteryBoxComponent> ent, FSMysteryBoxState state, TimeSpan duration)
    {
        ent.Comp.State = state;
        ent.Comp.StateStart = _timing.CurTime;
        ent.Comp.StateEnd = _timing.CurTime + duration;
        _light.SetEnabled(ent, state != FSMysteryBoxState.Idle);
        Dirty(ent);
    }

    private Dictionary<string, FSShopWeaponComponent> GetShops()
    {
        if (_shops != null)
            return _shops;

        _shops = new Dictionary<string, FSShopWeaponComponent>();
        var name = Factory.GetComponentName<FSShopWeaponComponent>();
        foreach (var proto in _protoMan.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.TryGetComponent<FSShopWeaponComponent>(name, out var shop) && shop.WeaponProtoId is { } weapon)
                _shops.TryAdd(weapon.Id, shop);
        }

        return _shops;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMysteryBoxComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State == FSMysteryBoxState.Idle || now < comp.StateEnd)
                continue;

            var ent = (uid, comp);
            switch (comp.State)
            {
                case FSMysteryBoxState.Spinning:
                    comp.Prize = comp.PendingPrize;
                    SetState(ent, FSMysteryBoxState.Offering, comp.OfferTime);
                    _audio.PlayPvs(comp.LandSound, uid);
                    _flair.Burst(LandBurst, Transform(uid).Coordinates.Offset(BoxCenter), BurstTint);
                    break;
                case FSMysteryBoxState.Offering:
                    Close(ent);
                    break;
                case FSMysteryBoxState.Closing:
                    SetState(ent, FSMysteryBoxState.Idle, TimeSpan.Zero);
                    break;
            }
        }
    }
}
