// Click a fortification with the Engineer's Hammer to pick an upgrade, then hammer it in: each hit pours in materials.
using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Engineering;
using Content.Server._FinalStand.Materials;
using Content.Server._FinalStand.Research;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Chat.Managers;
using Content.Server._FinalStand.Repair;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Popups;
using Content.Server._FinalStand.Effects;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Examine;
using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared._FinalStand.Particles;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Fortifications;

public sealed class FSUpgradeSystem : SharedFSUpgradeSystem
{
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private FSStructureHealthSystem _health = default!;
    [Dependency] private FSSiloNetworkSystem _silo = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ColorFlashEffectSystem _color = default!;
    [Dependency] private FSEngineeringResearchSystem _research = default!;
    [Dependency] private FSEngineeringTelemetrySystem _telemetry = default!;
    [Dependency] private FSFlairSystem _flair = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> UpgradeGlints = "FSUpgradeGlints";
    private static readonly ProtoId<FSParticleEmitterPrototype> UpgradeRing = "FSUpgradeRing";

    private static readonly TimeSpan InterruptWindow = TimeSpan.FromSeconds(3);
    private static readonly ProtoId<FSUpgradePrototype> Bulwark = "FSBranchBulwark";
    private const float OverbuiltBulwarkHealth = 1.1f;
    private static readonly TimeSpan HitDelay = TimeSpan.FromSeconds(0.7);
    private const float MaxBreachMultiplier = 2f;
    private static readonly SoundSpecifier PourSound = new SoundPathSpecifier("/Audio/_FinalStand/Structures/metalhit.ogg",
        AudioParams.Default.WithVariation(0.1f));
    private static readonly (int Wave, float Multiplier)[] WaveSteps = [(20, 2.2f), (10, 1.5f)];

    private static readonly EntProtoId HitEffect = "FSRivetSparks";
    private static readonly Color DoneFlash = Color.FromHex("#ffd27a");

    private static readonly TimeSpan StockInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextStock;
    private List<ProtoId<StackPrototype>>? _costMaterials;

    private static readonly SoundSpecifier DoneSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");
    private static readonly SoundSpecifier ChimeSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSUpgradeableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FSUpgradeableComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<FSUpgradeableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FSUpgradeableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FSUpgradeableComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FSUpgradeableComponent, FSHammerDoAfterEvent>(OnHammerDoAfter);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
        SubscribeLocalEvent<FSUpgradeableComponent, ExaminedEvent>(OnExamined);
        Subs.BuiEvents<FSUpgradeableComponent>(FSUpgradeUiKey.Key, subs => subs.Event<FSUpgradeSelectMessage>(OnSelect));
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextStock)
            return;

        _nextStock = _timing.CurTime + StockInterval;
        _costMaterials ??= Proto.EnumeratePrototypes<FSUpgradePrototype>().SelectMany(u => u.Cost.Keys).Distinct().ToList();

        var query = EntityQueryEnumerator<FSEngineerHammerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hammer, out var xform))
        {
            if (!HasComp<HandsComponent>(xform.ParentUid))
                continue;

            var stock = new Dictionary<string, int>();
            foreach (var stack in _costMaterials)
                stock[stack] = _silo.Sheets(xform.ParentUid, stack);

            if (stock.Count == hammer.SiloStock.Count && stock.All(kv => hammer.SiloStock.GetValueOrDefault(kv.Key) == kv.Value))
                continue;

            hammer.SiloStock = stock;
            Dirty(uid, hammer);
        }
    }

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (ev.NodeId != FSEngineeringNodes.OverbuiltBulwark)
            return;

        var query = EntityQueryEnumerator<FSUpgradeableComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Branch == Bulwark)
                Recalculate((uid, comp));
        }
    }

    private void OnMapInit(Entity<FSUpgradeableComponent> ent, ref MapInitEvent args)
    {
        if (_destructible.TryGetDestroyedAt(ent.Owner, out var max))
            ent.Comp.SteelHealth = max.Value.Float() / Proto.Index(ent.Comp.Grade).Health;

        var multiplier = WaveMultiplier();
        if (!multiplier.Equals(1f))
            SetWaveMultiplier(ent, multiplier);
    }

    private void OnExamined(Entity<FSUpgradeableComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(FSUpgradeSystem)))
        {
            var installed = new List<string> { Loc.GetString(Proto.Index(ent.Comp.Grade).Name) };
            if (ent.Comp.Branch is { } branch)
                installed.Add(Loc.GetString(Proto.Index(branch).Name));
            foreach (var module in ent.Comp.Modules)
                installed.Add(Loc.GetString(Proto.Index(module).Name));

            args.PushMarkup(Loc.GetString("fs-upgrade-examine-installed", ("list", string.Join(", ", installed))));

            if (ent.Comp.Pending is { } pending)
            {
                var upgrade = Proto.Index(pending);
                args.PushMarkup(Loc.GetString("fs-upgrade-examine-pending",
                    ("name", Loc.GetString(upgrade.Name)), ("done", ent.Comp.Poured), ("hits", upgrade.Hits)));
            }
            else if (_departments.IsEngineering(args.Examiner))
            {
                args.PushMarkup(Loc.GetString("fs-upgrade-examine-hint"));
            }
        }
    }

    private void OnPrepStarted(WavePrepStartedEvent args)
    {
        var multiplier = WaveMultiplier();
        var raised = false;
        var query = EntityQueryEnumerator<FSUpgradeableComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.WaveMultiplier.Equals(multiplier))
                continue;

            raised |= multiplier > comp.WaveMultiplier;
            SetWaveMultiplier((uid, comp), multiplier);
        }

        if (raised)
            _chat.DispatchServerAnnouncement(Loc.GetString("fs-upgrade-wave-hardened",
                ("percent", (int) MathF.Round((multiplier - 1f) * 100f))), Color.FromHex("#5fc8e8"));
    }

    private float WaveMultiplier()
    {
        if (!_waveRule.TryGetWaveStatus(out var wave, out _))
            return 1f;

        foreach (var (from, multiplier) in WaveSteps)
        {
            if (wave >= from)
                return multiplier;
        }

        return 1f;
    }

    private void OnDamaged(Entity<FSUpgradeableComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            ent.Comp.LastDamaged = _timing.CurTime;
    }

    private void OnGetVerbs(Entity<FSUpgradeableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<FSEngineerHammerComponent>(args.Using))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fs-upgrade-verb"),
            Act = () => OpenMenu(ent, user),
        });
    }

    private void OnInteractUsing(Entity<FSUpgradeableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<FSEngineerHammerComponent>(args.Used))
            return;

        args.Handled = true;
        Engage(ent, args.User, args.Used);
    }

    private void OnActivate(Entity<FSUpgradeableComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled
            || !_hands.TryGetActiveItem(args.User, out var held)
            || !HasComp<FSEngineerHammerComponent>(held))
            return;

        args.Handled = true;
        Engage(ent, args.User, held.Value);
    }

    // Click with the hammer: pick an upgrade if none is under way, otherwise keep hammering the one that is.
    private void Engage(Entity<FSUpgradeableComponent> ent, EntityUid user, EntityUid hammer)
    {
        if (ent.Comp.Pending == null)
            OpenMenu(ent, user);
        else
            StartHammering(ent, user, hammer);
    }

    private void OpenMenu(Entity<FSUpgradeableComponent> ent, EntityUid user)
    {
        if (!_departments.IsEngineering(user))
        {
            _popup.PopupEntity(Loc.GetString("fs-upgrade-engineers-only"), ent, user);
            return;
        }

        _ui.OpenUi(ent.Owner, FSUpgradeUiKey.Key, user);
    }

    private void OnSelect(Entity<FSUpgradeableComponent> ent, ref FSUpgradeSelectMessage args)
    {
        var user = args.Actor;
        if (!ent.Comp.Options.Contains(args.Upgrade)
            || !_departments.IsEngineering(user)
            || !_hands.TryGetActiveItem(user, out var held)
            || !HasComp<FSEngineerHammerComponent>(held))
            return;

        _ui.CloseUi(ent.Owner, FSUpgradeUiKey.Key, user);

        var upgrade = Proto.Index(args.Upgrade);
        var block = Check(ent.Comp, upgrade);
        if (block != FSUpgradeBlock.None)
        {
            _popup.PopupEntity(Loc.GetString("fs-upgrade-blocked-" + block.ToString().ToLowerInvariant()), ent, user);
            return;
        }

        if (upgrade.AwayFromDoors > 0f && NearDoor(ent, upgrade.AwayFromDoors))
        {
            _popup.PopupEntity(Loc.GetString("fs-upgrade-blocked-near-door"), ent, user);
            return;
        }

        if (ent.Comp.Pending != args.Upgrade)
        {
            ent.Comp.Pending = args.Upgrade;
            ent.Comp.Poured = 0;
            Dirty(ent);
        }

        StartHammering(ent, user, held.Value);
    }

    private void StartHammering(Entity<FSUpgradeableComponent> ent, EntityUid user, EntityUid hammer)
    {
        if (!_departments.IsEngineering(user))
        {
            _popup.PopupEntity(Loc.GetString("fs-upgrade-engineers-only"), ent, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, HitDelay, new FSHammerDoAfterEvent(), ent, ent, hammer)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
        });
    }

    private void OnHammerDoAfter(Entity<FSUpgradeableComponent> ent, ref FSHammerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        args.Repeat = PourHit(ent, args.User);
    }

    // One hammer hit. True while the upgrade still needs more hits and this one went in.
    public bool PourHit(Entity<FSUpgradeableComponent> target, EntityUid user)
    {
        if (target.Comp.Pending is not { } pendingId)
            return false;

        if (target.Comp.LastDamaged is { } last && _timing.CurTime - last < InterruptWindow)
        {
            _popup.PopupEntity(Loc.GetString("fs-upgrade-under-attack"), target, user, PopupType.SmallCaution);
            return false;
        }

        var upgrade = Proto.Index(pendingId);
        var cost = HitCost(upgrade, target.Comp.Poured);
        if (!_silo.TryConsume(user, cost))
        {
            _popup.PopupEntity(Loc.GetString("fs-upgrade-needs", ("list", FSMaterialText.List(Proto, cost))), target, user, PopupType.SmallCaution);
            return false;
        }

        _telemetry.RecordSpend("upgrades", cost);
        target.Comp.Poured++;
        _audio.PlayPvs(PourSound, target);
        Spawn(HitEffect, Transform(target).Coordinates);

        if (target.Comp.Poured < upgrade.Hits)
        {
            Dirty(target);
            return true;
        }

        Apply(target, upgrade);
        _audio.PlayPvs(DoneSound, target);
        var flash = upgrade.Color ?? DoneFlash;
        _flair.Burst(UpgradeRing, target, flash);
        _flair.Burst(UpgradeGlints, target, flash);
        _audio.PlayPvs(ChimeSound, target, AudioParams.Default.WithVolume(-4f).WithPitchScale(upgrade.Slot == FSUpgradeSlot.Grade ? 1.15f : 0.95f));
        _color.RaiseEffect(DoneFlash, [target.Owner], Filter.Pvs(target, entityManager: EntityManager));
        _popup.PopupEntity(Loc.GetString("fs-upgrade-done", ("name", Loc.GetString(upgrade.Name))), target, PopupType.Medium);
        return false;
    }

    public void Apply(Entity<FSUpgradeableComponent> ent, FSUpgradePrototype upgrade)
    {
        switch (upgrade.Slot)
        {
            case FSUpgradeSlot.Grade:
                ent.Comp.Grade = upgrade.ID;
                break;
            case FSUpgradeSlot.Branch:
                ent.Comp.Branch = upgrade.ID;
                break;
            case FSUpgradeSlot.Module:
                ent.Comp.Modules.Add(upgrade.ID);
                break;
        }

        ent.Comp.Pending = null;
        ent.Comp.Poured = 0;

        if (upgrade.Components != null)
            EntityManager.AddComponents(ent, upgrade.Components);

        if (upgrade.Sentry is { } sentry && TryComp<FSSentryTurretComponent>(ent, out var turret))
        {
            turret.MaxAmmo += sentry.Ammo;
            turret.Ammo += sentry.Ammo;
            turret.FireInterval *= sentry.FireInterval;
            turret.DamageMultiplier += sentry.Damage;
            turret.Range += sentry.Range;
            Dirty(ent, turret);
        }

        Recalculate(ent);
        Dirty(ent);
    }

    public void SetWaveMultiplier(Entity<FSUpgradeableComponent> ent, float multiplier)
    {
        ent.Comp.WaveMultiplier = multiplier;
        Recalculate(ent);
    }

    public float BreachMultiplier(EntityUid uid)
    {
        if (!TryComp<FSUpgradeableComponent>(uid, out var comp))
            return 1f;

        var multiplier = Proto.Index(comp.Grade).Breach;
        if (comp.Branch is { } branch)
            multiplier *= Proto.Index(branch).Breach;
        foreach (var module in comp.Modules)
            multiplier *= Proto.Index(module).Breach;

        return MathF.Min(multiplier, MaxBreachMultiplier);
    }

    private bool NearDoor(EntityUid uid, float range)
    {
        foreach (var door in _lookup.GetEntitiesInRange<DoorComponent>(Transform(uid).Coordinates, range))
        {
            if (door.Owner != uid)
                return true;
        }

        return false;
    }

    private void Recalculate(Entity<FSUpgradeableComponent> ent)
    {
        var grade = Proto.Index(ent.Comp.Grade);
        var branch = ent.Comp.Branch is { } id ? Proto.Index(id) : null;

        var health = ent.Comp.SteelHealth * grade.Health * (branch?.Health ?? 1f) * ent.Comp.WaveMultiplier;
        if (ent.Comp.Branch == Bulwark && _research.Has(FSEngineeringNodes.OverbuiltBulwark))
            health *= OverbuiltBulwarkHealth;
        ScaleDestruction(ent, health);
        _health.Refresh(ent.Owner);

        if (TryComp<FSPlateRepairComponent>(ent, out var repair))
        {
            repair.DelayMultiplier = grade.RepairDelay * (branch?.RepairDelay ?? 1f);
            if (grade.PlateMaterial is { } material)
                repair.Material = material;
        }
    }

    private void ScaleDestruction(EntityUid uid, float health)
    {
        if (health <= 0f
            || !TryComp<DestructibleComponent>(uid, out var destructible)
            || !_destructible.TryGetDestroyedAt((uid, destructible), out var current)
            || current.Value <= 0)
            return;

        var ratio = health / current.Value.Float();
        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger)
                trigger.Damage *= ratio;
        }
    }

    private static Dictionary<ProtoId<StackPrototype>, int> HitCost(FSUpgradePrototype upgrade, int hit)
    {
        var cost = new Dictionary<ProtoId<StackPrototype>, int>();
        foreach (var (material, total) in upgrade.Cost)
        {
            var amount = total * (hit + 1) / upgrade.Hits - total * hit / upgrade.Hits;
            if (amount > 0)
                cost[material] = amount;
        }

        return cost;
    }
}
