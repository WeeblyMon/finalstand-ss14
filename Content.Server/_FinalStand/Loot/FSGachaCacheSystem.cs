using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Loot;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Loot;

public sealed class FSGachaCacheSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SoundPathSpecifier OpenSound = new("/Audio/Machines/machine_vend.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGachaCacheComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<FSGachaCacheComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnInteractHand(Entity<FSGachaCacheComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryBuy(ent, args.User);
    }

    private void OnActivate(Entity<FSGachaCacheComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryBuy(ent, args.User);
    }

    private bool TryBuy(Entity<FSGachaCacheComponent> ent, EntityUid user)
    {
        if (ent.Comp.UsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-gacha-empty"), ent, user);
            return false;
        }

        if (!TryRoll(ent.Comp, out var prize, out var bonus))
        {
            Log.Error($"{ToPrettyString(ent)} has no resolvable loot pool; refusing to charge {ToPrettyString(user)}.");
            _popup.PopupEntity(Loc.GetString("fs-gacha-empty"), ent, user);
            return false;
        }

        if (!_mind.TryGetMind(user, out var mindId, out _) || !_wallet.TryDeductCredits(mindId, ent.Comp.Price))
        {
            _popup.PopupEntity(Loc.GetString("fs-gacha-no-funds"), ent, user);
            return false;
        }

        Loot(ent, user, prize, bonus);
        return true;
    }

    private bool TryRoll(FSGachaCacheComponent comp, out string prize, out string? bonus)
    {
        bonus = null;

        var roll = _random.NextFloat();
        var pool = roll < comp.RareChance
            ? comp.RarePool
            : roll < comp.RareChance + comp.UncommonChance
                ? comp.UncommonPool
                : comp.CommonPool;

        if (!TryPick(pool, out prize))
            return false;

        if (_random.Prob(comp.MiscChance) && TryPick(comp.MiscPool, out var extra))
            bonus = extra;

        return true;
    }

    private bool TryPick(ProtoId<WeightedRandomEntityPrototype> poolId, out string picked)
    {
        picked = string.Empty;

        if (!_protoMan.TryIndex(poolId, out var pool) || pool.Weights.Count == 0)
            return false;

        picked = _random.Pick(pool.Weights);
        return true;
    }

    private void Loot(Entity<FSGachaCacheComponent> ent, EntityUid user, string prize, string? bonus)
    {
        var comp = ent.Comp;
        var coordinates = Transform(ent).Coordinates;

        Spawn(prize, coordinates);

        if (bonus != null)
            Spawn(bonus, coordinates);

        comp.UsesLeft--;
        Dirty(ent);

        if (comp.UsesLeft <= 0)
            _appearance.SetData(ent, FSGachaCacheVisuals.Looted, true);

        _audio.PlayPvs(OpenSound, ent);
        _popup.PopupEntity(Loc.GetString("fs-gacha-opened"), ent, user);
    }
}
