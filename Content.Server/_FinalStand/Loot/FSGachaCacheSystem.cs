using Content.Server._FinalStand.Economy;
using Content.Server.Chat.Systems;
using Content.Shared._FinalStand.Loot;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Loot;

public sealed class FSGachaCacheSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SoundPathSpecifier AlarmSound = new("/Audio/Machines/alarm.ogg");
    private static readonly SoundPathSpecifier OpenSound = new("/Audio/Machines/machine_vend.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGachaCacheComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<FSGachaCacheComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FSGachaCacheComponent, FSGachaHackDoAfterEvent>(OnHacked);
        SubscribeLocalEvent<FSGachaCacheComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnInteractHand(Entity<FSGachaCacheComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartHack(ent, args.User);
    }

    private void OnActivate(Entity<FSGachaCacheComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartHack(ent, args.User);
    }

    private bool TryStartHack(Entity<FSGachaCacheComponent> ent, EntityUid user)
    {
        if (ent.Comp.UsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-gacha-empty"), ent, user);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("fs-gacha-hack-start"), ent, user);

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.HackDuration,
            new FSGachaHackDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnHacked(Entity<FSGachaCacheComponent> ent, ref FSGachaHackDoAfterEvent args)
    {
        if (args.Cancelled || ent.Comp.UsesLeft <= 0)
            return;

        Loot(ent, args.Args.User);

        _audio.PlayPvs(AlarmSound, ent, AudioParams.Default.WithVolume(2f).WithMaxDistance(28f));
        _chat.DispatchGlobalAnnouncement(Loc.GetString("fs-gacha-alarm"), Loc.GetString("fs-gacha-alarm-sender"),
            playSound: false, colorOverride: Color.OrangeRed);
    }

    private void OnGetVerbs(Entity<FSGachaCacheComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.UsesLeft <= 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fs-gacha-verb-buy", ("price", ent.Comp.Price)),
            Act = () => TryBuy(ent, user),
        });
    }

    private void TryBuy(Entity<FSGachaCacheComponent> ent, EntityUid user)
    {
        if (ent.Comp.UsesLeft <= 0)
            return;

        if (!_mind.TryGetMind(user, out var mindId, out _) || !_wallet.TryDeductCredits(mindId, ent.Comp.Price))
        {
            _popup.PopupEntity(Loc.GetString("fs-gacha-no-funds"), ent, user);
            return;
        }

        Loot(ent, user);
    }

    private void Loot(Entity<FSGachaCacheComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;
        var coordinates = Transform(ent).Coordinates;

        var roll = _random.NextFloat();
        var pool = roll < comp.RareChance
            ? comp.RarePool
            : roll < comp.RareChance + comp.UncommonChance
                ? comp.UncommonPool
                : comp.CommonPool;

        SpawnFrom(pool, coordinates);

        if (_random.Prob(comp.MiscChance))
            SpawnFrom(comp.MiscPool, coordinates);

        comp.UsesLeft--;
        Dirty(ent);

        if (comp.UsesLeft <= 0)
            _appearance.SetData(ent, FSGachaCacheVisuals.Looted, true);

        _audio.PlayPvs(OpenSound, ent);
        _popup.PopupEntity(Loc.GetString("fs-gacha-opened"), ent, user);
    }

    private void SpawnFrom(ProtoId<WeightedRandomEntityPrototype> poolId, EntityCoordinates coordinates)
    {
        if (!_protoMan.TryIndex(poolId, out var pool) || pool.Weights.Count == 0)
            return;

        Spawn(_random.Pick(pool.Weights), coordinates);
    }
}
