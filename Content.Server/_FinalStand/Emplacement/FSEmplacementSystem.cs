// The Chief Engineer's Defensive Emplacement: unfolds whole, feeds its hardpoint sentries, covers whoever stands inside, and folds back to the CE when the wave ends.
using Content.Server._FinalStand.Deployables;
using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Engineering;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.NPC;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Emplacement;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Placement;
using Content.Shared.Alert;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Emplacement;

public sealed partial class FSEmplacementSystem : EntitySystem
{
    [Dependency] private FSEmplacementRuleSystem _rules = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private FSDeployableSystem _deployable = default!;
    [Dependency] private FSBreachEvaluator _breach = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSEngineeringResearchSystem _research = default!;
    [Dependency] private Robust.Shared.Audio.Systems.SharedAudioSystem _audio = default!;
    [Dependency] private Content.Server._FinalStand.Effects.FSFlairSystem _flair = default!;

    private static readonly ProtoId<AlertPrototype> CoverAlert = "FSEmplacementCover";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private const float MeleeRange = 1.5f;
    private const float ReinforcedSandbagDamage = 2f / 3f;
    private const float UnfoldStagger = 0.08f;
    private static readonly SoundSpecifier UnfoldSound = new SoundPathSpecifier("/Audio/Effects/box_deploy.ogg");
    private static readonly SoundSpecifier FoldSound = new SoundPathSpecifier("/Audio/Items/zip.ogg");

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), FSEmplacementComponent> _coverTiles = new();
    private readonly Dictionary<EntityUid, float> _covered = new();
    private TimeSpan _next;
    private float _feedCarry;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSEmplacementKitComponent, FSPlacementConfirmedEvent>(OnPlace);
        SubscribeLocalEvent<FSEmplacementComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FSEmplacementComponent, FSEmplacementPackDoAfterEvent>(OnPackDoAfter);
        SubscribeLocalEvent<FSEmplacementComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FSEmplacementComponent, EntityTerminatingEvent>(OnCoreTerminating);
        SubscribeLocalEvent<FSEmplacementCoverComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<FSEmplacementPieceComponent, DamageModifyEvent>(OnPieceDamageModify);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnPlace(Entity<FSEmplacementKitComponent> kit, ref FSPlacementConfirmedEvent args)
    {
        if (!_departments.IsChiefEngineer(args.User))
        {
            _popup.PopupEntity(Loc.GetString("fs-emplacement-ce-only"), args.User, args.User);
            return;
        }

        var center = _rules.Snap(args.Coordinates);
        if (GetBlockReason(kit, args.User, center, args.Direction) is { } reason)
        {
            _popup.PopupEntity(reason, args.User, args.User);
            return;
        }

        var core = Spawn(kit.Comp.Core, center);
        _transform.SetLocalRotation(core, args.Direction.ToAngle());
        var comp = Comp<FSEmplacementComponent>(core);
        comp.Deployed = true;
        comp.OwnerMind = _mind.TryGetMind(args.User, out var mindId, out _) ? mindId : null;

        foreach (var piece in Pieces(kit.Comp))
        {
            var uid = Spawn(piece.Proto, FSEmplacementRuleSystem.TileOf(center, piece.Offset, args.Direction));
            _transform.SetLocalRotation(uid, FSEmplacementRuleSystem.RotationOf(piece, args.Direction));
            if (!Transform(uid).Anchored)
                _transform.AnchorEntity(uid);

            EnsureComp<FSEmplacementPieceComponent>(uid).Core = core;
            var deployedBy = EnsureComp<FSDeployedByComponent>(uid);
            deployedBy.OwnerMind = comp.OwnerMind;
            deployedBy.DeployedBy = args.User;
            deployedBy.SourceProto = piece.Proto;
            comp.Pieces.Add(uid);
        }

        QueueDel(kit);
        args.Handled = true;
        _audio.PlayPvs(UnfoldSound, core);
        for (var i = 0; i < comp.Pieces.Count; i++)
            _flair.Burst("FSDustPuff", comp.Pieces[i], delay: i * UnfoldStagger, count: 0.6f);
        _flair.Burst("FSDustRing", core, delay: comp.Pieces.Count * UnfoldStagger, kick: 0.5f);
        _popup.PopupEntity(Loc.GetString("fs-emplacement-placed"), core, args.User);
    }

    private IEnumerable<FSEmplacementPiece> Pieces(FSEmplacementKitComponent kit)
    {
        foreach (var piece in kit.Pieces)
            yield return piece;

        if (kit.ResearchHardpoint is { } extra && _research.Has(FSEngineeringNodes.ThirdHardpoint))
            yield return extra;
    }

    private void OnPieceDamageModify(Entity<FSEmplacementPieceComponent> ent, ref DamageModifyEvent args)
    {
        if (!HasComp<FSSentryTurretComponent>(ent) && _research.Has(FSEngineeringNodes.ReinforcedSandbags))
            args.Damage *= ReinforcedSandbagDamage;
    }

    private string? GetBlockReason(Entity<FSEmplacementKitComponent> kit, EntityUid user, EntityCoordinates center, Direction facing)
    {
        var range = CompOrNull<FSPlaceableComponent>(kit)?.Range ?? 0f;
        if (!_rules.CanPlace(user, center, facing, kit.Comp, range))
            return Loc.GetString("fs-emplacement-no-room");

        foreach (var tile in _rules.Footprint(center, facing, kit.Comp))
        {
            if (_breach.IsAdjacentToSpace(tile))
                return Loc.GetString("fs-rcd-next-to-space");
        }

        foreach (var piece in kit.Comp.Pieces)
        {
            var tile = FSEmplacementRuleSystem.TileOf(center, piece.Offset, facing);
            if (_deployable.IsOccupied(tile, FSEmplacementRuleSystem.RotationOf(piece, facing).GetCardinalDir()))
                return Loc.GetString("fs-deployable-occupied");
        }

        return null;
    }

    private void OnGetVerbs(Entity<FSEmplacementComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fs-emplacement-pack-verb"),
            Act = () => TryStartPack(ent, user),
        });
    }

    private void TryStartPack(Entity<FSEmplacementComponent> ent, EntityUid user)
    {
        if (!CanPack(ent, user))
        {
            _popup.PopupEntity(Loc.GetString("fs-emplacement-not-yours"), ent, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.PackTime, new FSEmplacementPackDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("fs-emplacement-packing"), ent, user);
    }

    public bool CanPack(Entity<FSEmplacementComponent> ent, EntityUid user)
    {
        return _departments.IsChiefEngineer(user)
               || _mind.TryGetMind(user, out var mindId, out _) && mindId == ent.Comp.OwnerMind;
    }

    private void OnPackDoAfter(Entity<FSEmplacementComponent> ent, ref FSEmplacementPackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !CanPack(ent, args.User))
            return;

        args.Handled = true;
        var kit = Pack(ent);
        Give(args.User, kit);
    }

    public EntityUid Pack(Entity<FSEmplacementComponent> ent)
    {
        ent.Comp.Packed = true;
        var kit = Spawn(ent.Comp.Kit, _transform.GetMapCoordinates(ent));
        _audio.PlayPvs(FoldSound, kit);
        foreach (var piece in ent.Comp.Pieces)
            QueueDel(piece);

        if (!TerminatingOrDeleted(ent))
            QueueDel(ent);

        return kit;
    }

    private void OnCoreTerminating(Entity<FSEmplacementComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.Deployed || ent.Comp.Packed || Transform(ent).MapUid is not { } map || TerminatingOrDeleted(map))
            return;

        var kit = Pack(ent);
        ReturnToEngineers(kit, ent.Comp.OwnerMind);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var query = EntityQueryEnumerator<FSEmplacementComponent>();
        var cores = new List<Entity<FSEmplacementComponent>>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Packed)
                cores.Add((uid, comp));
        }

        foreach (var core in cores)
        {
            var kit = Pack(core);
            if (ReturnToEngineers(kit, core.Comp.OwnerMind) is { } holder)
                _popup.PopupEntity(Loc.GetString("fs-emplacement-returned"), holder, holder, PopupType.Medium);
        }
    }

    private EntityUid? ReturnToEngineers(EntityUid kit, EntityUid? ownerMind)
    {
        if (ownerMind != null
            && TryComp<MindComponent>(ownerMind, out var mind)
            && mind.OwnedEntity is { } body
            && !_mobState.IsDead(body))
        {
            Give(body, kit);
            return body;
        }

        EntityUid? fallback = null;
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid) || !_departments.IsEngineering(uid))
                continue;

            if (_departments.IsChiefEngineer(uid))
            {
                Give(uid, kit);
                return uid;
            }

            fallback ??= uid;
        }

        if (fallback is { } engineer)
            Give(engineer, kit);

        return fallback;
    }

    private void Give(EntityUid user, EntityUid kit)
    {
        if (_hands.TryPickupAnyHand(user, kit, checkActionBlocker: false, animate: false))
            return;

        if (_inventory.TryGetSlotEntity(user, "back", out var bag)
            && _storage.Insert(bag.Value, kit, out _, user: user, playSound: false))
            return;

        _transform.SetCoordinates(kit, Transform(user).Coordinates);
    }

    private void OnExamined(Entity<FSEmplacementComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fs-emplacement-examine-reserve", ("rounds", ent.Comp.Reserve)));
        args.PushMarkup(Loc.GetString("fs-emplacement-examine-pack"));
    }

    private void OnDamageModify(Entity<FSEmplacementCoverComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is { } origin && _transform.InRange(origin, ent.Owner, MeleeRange))
            args.Damage *= ent.Comp.MeleeDamage;
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Interval;
        _feedCarry += (float) Interval.TotalSeconds;
        var feedSeconds = (int) _feedCarry;
        _feedCarry -= feedSeconds;

        _coverTiles.Clear();
        _covered.Clear();

        var cores = EntityQueryEnumerator<FSEmplacementComponent, TransformComponent>();
        while (cores.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.Pieces.RemoveAll(p => TerminatingOrDeleted(p));
            if (feedSeconds > 0)
                Feed((uid, comp), comp.FeedPerSecond * feedSeconds);

            if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                _coverTiles[(gridUid, center + new Vector2i(x, y))] = comp;
        }

        if (_coverTiles.Count > 0)
        {
            var players = EntityQueryEnumerator<FSFriendlyFireComponent, TransformComponent>();
            while (players.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.GridUid is { } gridUid
                    && TryComp<MapGridComponent>(gridUid, out var grid)
                    && _coverTiles.TryGetValue((gridUid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates)), out var core))
                    _covered[uid] = core.CoverMelee;
            }
        }

        var buffed = EntityQueryEnumerator<FSEmplacementCoverComponent>();
        while (buffed.MoveNext(out var uid, out _))
        {
            if (_covered.ContainsKey(uid))
                continue;

            RemComp<FSEmplacementCoverComponent>(uid);
            _alerts.ClearAlert(uid, CoverAlert);
        }

        foreach (var (player, melee) in _covered)
        {
            if (!TryComp<FSEmplacementCoverComponent>(player, out var cover))
            {
                cover = AddComp<FSEmplacementCoverComponent>(player);
                _alerts.ShowAlert(player, CoverAlert);
            }

            cover.MeleeDamage = melee;
        }
    }

    private void Feed(Entity<FSEmplacementComponent> core, int budget)
    {
        foreach (var piece in core.Comp.Pieces)
        {
            if (core.Comp.Reserve <= 0)
                return;

            if (!TryComp<FSSentryTurretComponent>(piece, out var sentry) || sentry.Ammo >= sentry.MaxAmmo)
                continue;

            var amount = Math.Min(Math.Min(sentry.MaxAmmo - sentry.Ammo, budget), core.Comp.Reserve);
            sentry.Ammo += amount;
            core.Comp.Reserve -= amount;
            Dirty(piece, sentry);
        }
    }
}
