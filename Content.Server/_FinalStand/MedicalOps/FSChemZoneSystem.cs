using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Smoking;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSChemZoneSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSMoneyOnHitSystem _money = default!;
    [Dependency] private FSChemCreditSystem _credit = default!;
    [Dependency] private FSChemLauncherSystem _launcher = default!;
    [Dependency] private FSMedicalUpgradeSystem _upgrades = default!;
    [Dependency] private FSMedicalStatsSystem _stats = default!;

    private static readonly EntProtoId ZoneProto = "FSChemZone";
    private static readonly ProtoId<DamageTypePrototype> Caustic = "Caustic";

    private static readonly SoundSpecifier BurstSound = new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/flask_burst.ogg");
    private static readonly SoundSpecifier CaughtSound = new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/dart_hit.ogg");

    private static readonly TimeSpan TagWindow = TimeSpan.FromSeconds(10);

    private const int MoneyPerTick = 5;
    private const float CatalystPerAlly = 4f;
    private const float CatalystPerDamage = 0.1f;
    private const float CatalysedRadius = 1.6f;
    private const float CatalysedLifetime = 1.5f;
    private const float CatalysedScale = 1.5f;
    private const float ChainBurstDamage = 40f;
    private const float ChainRadius = 1.5f;
    private const float ChainLifetime = 3f;
    private const float PanaceaFraction = 0.25f;
    private const int MaxCloudExtent = 6;

    private readonly Dictionary<ProtoId<ReagentPrototype>, EntityEffect[]> _touch = new();
    private readonly Dictionary<ProtoId<ReagentPrototype>, FSChemFormulaPrototype> _formulas = new();
    private bool _formulasBuilt;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly List<Entity<FSChemZoneComponent>> _ticking = new();
    private readonly List<PendingChain> _chains = new();

    private readonly record struct PendingChain(MapCoordinates Coords, List<ProtoId<ReagentPrototype>> Reagents,
        EntityUid? Chemist, EntityUid? Launcher, string? Label);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSChemCanisterComponent, LandEvent>(OnCanisterLand);
        SubscribeLocalEvent<FSChemCanisterComponent, ThrowDoHitEvent>(OnCanisterHit);
        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnEnemyDied);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReagentPrototype>())
            _touch.Clear();

        if (args.WasModified<FSChemFormulaPrototype>())
        {
            _formulas.Clear();
            _formulasBuilt = false;
        }
    }

    private void OnCanisterLand(Entity<FSChemCanisterComponent> ent, ref LandEvent args)
    {
        Burst(ent, args.User);
    }

    // Canisters burst on the first mob of the side they are meant for, so a stim lands on the squad and acid on the horde.
    private void OnCanisterHit(Entity<FSChemCanisterComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target) || !_prototypes.TryIndex(ent.Comp.Formula, out var formula))
            return;

        var crew = HasComp<FSFriendlyFireComponent>(args.Target);
        if (crew != (formula.Kind == FSChemFormulaKind.Support))
            return;

        Burst(ent, args.Component.Thrower);
    }

    private void Burst(Entity<FSChemCanisterComponent> ent, EntityUid? thrower)
    {
        if (ent.Comp.Burst || TerminatingOrDeleted(ent))
            return;

        ent.Comp.Burst = true;

        if (_prototypes.TryIndex(ent.Comp.Formula, out var formula))
        {
            SpawnZone(_xform.GetMapCoordinates(ent),
                [formula.Reagent],
                thrower,
                ent.Comp.Launcher,
                ent.Comp.Catalysed,
                Loc.GetString(formula.Name));
        }

        QueueDel(ent);
    }

    public EntityUid? SpawnZone(MapCoordinates coords,
        IReadOnlyCollection<ProtoId<ReagentPrototype>> reagents,
        EntityUid? owner,
        EntityUid? launcher,
        bool catalysed,
        string? label,
        float? radius = null,
        float? lifetime = null)
    {
        if (reagents.Count == 0
            || !_map.TryFindGridAt(coords, out var gridUid, out var grid)
            || !_map.TryGetTileRef(gridUid, grid, coords.Position, out var tile)
            || _turf.IsSpace(tile))
            return null;

        var uid = Spawn(ZoneProto, coords);
        var zone = EnsureComp<FSChemZoneComponent>(uid);
        zone.Reagents.AddRange(reagents);
        zone.Chemist = owner;
        zone.Launcher = launcher;
        zone.Label = label;
        zone.Radius = radius ?? _upgrades.ZoneRadius(zone.Radius);
        zone.Lifetime = lifetime ?? _upgrades.ZoneLifetime(zone.Lifetime);

        var hostile = reagents.Any(r => KindOf(r) == FSChemFormulaKind.Hostile);
        if (catalysed)
        {
            zone.Catalysed = true;
            zone.Radius *= CatalysedRadius;
            zone.Lifetime *= CatalysedLifetime;
            zone.Scale = CatalysedScale;
            zone.Chains = hostile && _upgrades.Unlocked(FSMedicalUpgradeSystem.ChainReaction);
        }

        zone.Accumulator = zone.TickInterval;
        EnsureComp<TimedDespawnComponent>(uid).Lifetime = zone.Lifetime;

        SpawnClouds((uid, zone), CloudColour(reagents));
        _audio.PlayPvs(BurstSound, uid);

        if (zone.Chains)
            ChainBurst((uid, zone));

        return uid;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _ticking.Clear();
        var query = EntityQueryEnumerator<FSChemZoneComponent>();
        while (query.MoveNext(out var uid, out var zone))
        {
            zone.Accumulator += frameTime;
            if (zone.Accumulator < zone.TickInterval)
                continue;

            zone.Accumulator -= zone.TickInterval;
            _ticking.Add((uid, zone));
        }

        foreach (var zone in _ticking)
        {
            if (!TerminatingOrDeleted(zone))
                Tick(zone);
        }

        if (_chains.Count == 0)
            return;

        var chains = _chains.ToArray();
        _chains.Clear();
        foreach (var chain in chains)
            SpawnZone(chain.Coords, chain.Reagents, chain.Chemist, chain.Launcher, false, chain.Label, ChainRadius, ChainLifetime);
    }

    private void Tick(Entity<FSChemZoneComponent> zone)
    {
        var coords = _xform.GetMapCoordinates(zone);
        var ownerMind = OwnerMind(zone.Comp.Chemist);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(coords, zone.Comp.Radius, _mobs);

        var caught = 0;
        foreach (var mob in _mobs)
        {
            if (TerminatingOrDeleted(mob) || _mobState.IsDead(mob))
                continue;

            var crew = HasComp<FSFriendlyFireComponent>(mob);
            foreach (var reagent in zone.Comp.Reagents)
            {
                var kind = KindOf(reagent);
                if (kind == FSChemFormulaKind.Support && !crew || kind == FSChemFormulaKind.Hostile && crew)
                    continue;

                var effects = TouchEffects(reagent);
                if (effects.Length == 0)
                    continue;

                if (crew)
                {
                    if (Support(zone, mob, effects, ownerMind))
                        caught++;
                }
                else
                {
                    Hostile(zone, mob, effects, reagent, ownerMind);
                }
            }
        }

        if (caught == 0 || zone.Comp.Chemist is not { } owner || !Exists(owner))
            return;

        _launcher.AddCatalyst(zone.Comp.Launcher, CatalystPerAlly * caught);
        _audio.PlayEntity(CaughtSound, owner, owner, AudioParams.Default.WithVolume(-6f));
        if (zone.Comp.Label is { } label)
        {
            _popup.PopupCoordinates(Loc.GetString("fs-chem-zone-caught", ("formula", label), ("count", zone.Comp.Caught.Count)),
                Transform(zone).Coordinates, owner);
        }
    }

    // True the first time this cloud reaches someone other than the chemist who fired it.
    private bool Support(Entity<FSChemZoneComponent> zone, EntityUid crew, EntityEffect[] effects, EntityUid? ownerMind)
    {
        _effects.ApplyEffects(crew, effects, zone.Comp.Scale, zone.Comp.Chemist);

        if (zone.Comp.Catalysed && _upgrades.Unlocked(FSMedicalUpgradeSystem.Panacea))
            Panacea(zone, crew);

        if (zone.Comp.Chemist is not { } owner || crew == owner || !zone.Comp.Caught.Add(crew))
            return false;

        if (effects.Any(e => e is FSApplyCombatBuff or FSApplyArmorPiercing))
            _credit.RegisterDelivery(crew, owner, FSMedicalPayoutRates.SplashClaimBudget);

        if (ownerMind is { } mind)
            _stats.RecordChem(mind, dosed: 1);

        return true;
    }

    private void Panacea(Entity<FSChemZoneComponent> zone, EntityUid crew)
    {
        if (!_thresholds.TryGetThresholdForState(crew, MobState.Critical, out var crit) || zone.Comp.Lifetime <= 0f)
            return;

        var perTick = crit.Value.Float() * PanaceaFraction * zone.Comp.TickInterval / zone.Comp.Lifetime;
        _damageable.HealDistributed(crew, FixedPoint2.New(-perTick), origin: zone.Comp.Chemist);
        _effects.ApplyEffect(crew, new FSStaunchBleeding());
    }

    private void Hostile(Entity<FSChemZoneComponent> zone, EntityUid target, EntityEffect[] effects,
        ProtoId<ReagentPrototype> reagent, EntityUid? ownerMind)
    {
        var before = _damageable.GetTotalDamage(target);

        if (zone.Comp.Chemist != null)
            _money.SetFlatHit(target, MoneyPerTick);

        _effects.ApplyEffects(target, effects, zone.Comp.Scale, zone.Comp.Chemist);
        _money.ClearFlatHit();

        if (zone.Comp.Chemist is not { } owner || TerminatingOrDeleted(target))
            return;

        var dealt = MathF.Max(0f, (_damageable.GetTotalDamage(target) - before).Float());
        Tag(target, owner, zone.Comp.Launcher, dealt, IsDebuff(reagent));

        if (dealt <= 0f)
            return;

        _launcher.AddCatalyst(zone.Comp.Launcher, dealt * CatalystPerDamage);
        if (ownerMind is { } mind)
            _stats.RecordChem(mind, damage: dealt);
    }

    private void Tag(EntityUid target, EntityUid chemist, EntityUid? launcher, float dealt, bool debuff)
    {
        var tag = EnsureComp<FSChemTaggedComponent>(target);
        if (tag.Chemist != chemist)
        {
            tag.Chemist = chemist;
            tag.Damage = 0f;
            tag.Debuffed = false;
        }

        tag.Launcher = launcher;
        tag.Until = _timing.CurTime + TagWindow;
        tag.Damage += dealt;
        tag.Debuffed |= debuff;
    }

    private void ChainBurst(Entity<FSChemZoneComponent> zone)
    {
        var burst = new FSHarmHostiles
        {
            Damage = new DamageSpecifier(_prototypes.Index(Caustic), FixedPoint2.New(ChainBurstDamage)),
        };

        _mobs.Clear();
        _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(zone), zone.Comp.Radius, _mobs);
        var ownerMind = OwnerMind(zone.Comp.Chemist);

        foreach (var mob in _mobs)
        {
            if (!HasComp<FSFriendlyFireComponent>(mob) && !_mobState.IsDead(mob))
                Hostile(zone, mob, [burst], zone.Comp.Reagents[0], ownerMind);
        }
    }

    private void OnEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (TerminatingOrDeleted(args.Enemy))
            return;

        var corpse = _xform.GetMapCoordinates(args.Enemy);
        var query = EntityQueryEnumerator<FSChemZoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zone, out var xform))
        {
            if (!zone.Chains || xform.MapID != corpse.MapId)
                continue;

            var centre = _xform.GetWorldPosition(xform);
            if ((centre - corpse.Position).LengthSquared() > zone.Radius * zone.Radius)
                continue;

            _chains.Add(new PendingChain(corpse, zone.Reagents.ToList(), zone.Chemist, zone.Launcher, zone.Label));
            return;
        }
    }

    private void SpawnClouds(Entity<FSChemZoneComponent> zone, Color colour)
    {
        var origin = Transform(zone).Coordinates;
        var extent = Math.Min((int) MathF.Ceiling(zone.Comp.Radius), MaxCloudExtent);

        for (var x = -extent; x <= extent; x++)
        {
            for (var y = -extent; y <= extent; y++)
            {
                var offset = new System.Numerics.Vector2(x, y);
                if (offset.Length() > zone.Comp.Radius)
                    continue;

                var cloud = Spawn(zone.Comp.CloudProto, origin.Offset(offset));
                EnsureComp<TimedDespawnComponent>(cloud).Lifetime = zone.Comp.Lifetime;
                _appearance.SetData(cloud, SmokeVisuals.Color, colour);
            }
        }
    }

    private Color CloudColour(IEnumerable<ProtoId<ReagentPrototype>> reagents)
    {
        foreach (var reagent in reagents)
        {
            if (_prototypes.TryIndex(reagent, out var proto))
                return proto.SubstanceColor.WithAlpha(0.45f);
        }

        return Color.White.WithAlpha(0.45f);
    }

    private EntityUid? OwnerMind(EntityUid? owner)
        => owner is { } uid && _mind.TryGetMind(uid, out var mind, out _) ? mind : null;

    private FSChemFormulaPrototype? FormulaFor(ProtoId<ReagentPrototype> reagent)
    {
        if (!_formulasBuilt)
        {
            foreach (var formula in _prototypes.EnumeratePrototypes<FSChemFormulaPrototype>())
                _formulas[formula.Reagent] = formula;

            _formulasBuilt = true;
        }

        return _formulas.GetValueOrDefault(reagent);
    }

    // Reagents with no formula, like whatever a doctor pours into a flask, touch everyone in the cloud.
    public FSChemFormulaKind? KindOf(ProtoId<ReagentPrototype> reagent) => FormulaFor(reagent)?.Kind;

    private bool IsDebuff(ProtoId<ReagentPrototype> reagent) => FormulaFor(reagent)?.Debuff ?? false;

    private EntityEffect[] TouchEffects(ProtoId<ReagentPrototype> reagent)
    {
        if (_touch.TryGetValue(reagent, out var cached))
            return cached;

        var effects = new List<EntityEffect>();
        if (_prototypes.TryIndex(reagent, out var proto) && proto.ReactiveEffects != null)
        {
            foreach (var entry in proto.ReactiveEffects.Values)
            {
                if (entry.Methods.Contains(ReactionMethod.Touch))
                    effects.AddRange(entry.Effects);
            }
        }

        return _touch[reagent] = effects.ToArray();
    }
}
