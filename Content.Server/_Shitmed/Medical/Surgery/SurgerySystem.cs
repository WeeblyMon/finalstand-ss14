
using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.MedicalOps;
using Content.Server.Atmos.Rotting;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Body;
using Content.Server.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Weapons.Melee.Events;
using System.Linq;

namespace Content.Server._Shitmed.Medical.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private OrganLookupSystem _lookup = default!;
    [Dependency] private BodyAppearanceSystem _body = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private FSSurgeryGuideSystem _guide = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _gameTiming = default!;

    private static readonly TimeSpan OpenRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, string> _openWindows = new();
    private TimeSpan _nextOpenRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryStepDamageEvent>(OnSurgeryStepDamage);
        SubscribeLocalEvent<SurgeryDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnSurgeryDamageChange);
        SubscribeLocalEvent<SurgerySpecialDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnSurgerySpecialDamageChange);
        SubscribeLocalEvent<SurgeryDamageUserComponent, SurgeryStepEvent>(OnSurgeryDamageUser);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepScreamComplete);
        SubscribeLocalEvent<SurgeryStepSpawnEffectComponent, SurgeryStepEvent>(OnStepSpawnComplete);
        SubscribeLocalEvent<SurgeryTargetComponent, BoundUIOpenedEvent>(OnWindowOpened);
        SubscribeLocalEvent<SurgeryTargetComponent, BoundUIClosedEvent>(OnWindowClosed);
    }

    private void OnWindowOpened(Entity<SurgeryTargetComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is SurgeryUIKey)
            _openWindows.TryAdd(ent.Owner, string.Empty);
    }

    private void OnWindowClosed(Entity<SurgeryTargetComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is SurgeryUIKey && !_ui.IsUiOpen(ent.Owner, SurgeryUIKey.Key))
            _openWindows.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_openWindows.Count == 0 || _gameTiming.CurTime < _nextOpenRefresh)
            return;

        _nextOpenRefresh = _gameTiming.CurTime + OpenRefreshInterval;

        foreach (var body in _openWindows.Keys.ToList())
        {
            if (TerminatingOrDeleted(body) || !_ui.IsUiOpen(body, SurgeryUIKey.Key))
            {
                _openWindows.Remove(body);
                continue;
            }

            var (choices, unavailable) = BuildOptions(body);
            var key = OptionsKey(choices, unavailable);
            if (_openWindows[body] == key)
                continue;

            SendOptions(body, choices, unavailable, key);
        }
    }

    protected override void RefreshUI(EntityUid body)
    {
        var (choices, unavailable) = BuildOptions(body);
        SendOptions(body, choices, unavailable, OptionsKey(choices, unavailable));
    }

    public (Dictionary<NetEntity, List<EntProtoId>> Choices, Dictionary<NetEntity, List<EntProtoId>> Unavailable)
        BuildOptions(EntityUid body)
    {
        var choices = new Dictionary<NetEntity, List<EntProtoId>>();
        var unavailableByPart = new Dictionary<NetEntity, List<EntProtoId>>();

        foreach (var part in _lookup.GetBodyOrgans(body))
        {
            if (OrganCategories.ToTarget(part.Comp.Category) == null)
                continue;

            var valid = new List<EntProtoId>();
            var unavailable = new List<EntProtoId>();
            foreach (var surgery in AllSurgeries)
            {
                var role = _guide.RoleOf(surgery.Id);
                if (role == FSSurgeryRole.Hidden || GetSingleton(surgery) is not { } surgeryEnt)
                    continue;

                var ev = new SurgeryValidEvent(body, part.Owner);
                RaiseLocalEvent(surgeryEnt, ref ev);

                if (!ev.Cancelled)
                    valid.Add(surgery);
                else if (role is FSSurgeryRole.Goal or FSSurgeryRole.Elective)
                    unavailable.Add(surgery);
            }

            choices[GetNetEntity(part.Owner)] = valid;
            unavailableByPart[GetNetEntity(part.Owner)] = unavailable;
        }

        return (choices, unavailableByPart);
    }

    private static string OptionsKey(Dictionary<NetEntity, List<EntProtoId>> choices, Dictionary<NetEntity, List<EntProtoId>> unavailable)
    {
        return string.Join(';', choices.OrderBy(kv => kv.Key.Id)
                   .Select(kv => $"{kv.Key.Id}:{string.Join(',', kv.Value)}"))
               + "|"
               + string.Join(';', unavailable.OrderBy(kv => kv.Key.Id)
                   .Select(kv => $"{kv.Key.Id}:{string.Join(',', kv.Value)}"));
    }

    private void SendOptions(EntityUid body,
        Dictionary<NetEntity, List<EntProtoId>> choices,
        Dictionary<NetEntity, List<EntProtoId>> unavailable,
        string key)
    {
        if (_openWindows.ContainsKey(body))
            _openWindows[body] = key;

        _ui.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(choices, unavailable));
        /*
            Reason we do this is because when applying a BUI State, it rolls back the state on the entity temporarily,
            which just so happens to occur right as we're checking for step completion, so we end up with the UI
            not updating at all until you change tools or reopen the window. I love shitcode.
        */
        _ui.ServerSendUiMessage(body, SurgeryUIKey.Key, new SurgeryBuiRefreshMessage());
    }

    private DamageGroupPrototype? GetDamageGroupByType(string id)
    {
        return (from @group in _prototypes.EnumeratePrototypes<DamageGroupPrototype>() where @group.DamageTypes.Contains(id) select @group).FirstOrDefault();
    }

    private void SetDamage(EntityUid body,
        DamageSpecifier damage,
        float partMultiplier,
        EntityUid user,
        EntityUid part,
        bool affectAll = false)
    {
        if (!TryComp<OrganComponent>(part, out var partComp))
            return;

        // kinda funky but still works TODO: Also the scar treating surgery too, fuck.
        _wounds.TryHaltAllBleeding(part, force: true);

        _damageable.TryChangeDamage(body,
            damage,
            true,
            origin: user,
            targetPart: affectAll ? TargetBodyPart.All : _lookup.GetTarget(part) ?? TargetBodyPart.Chest);
    }

    private void OnSurgeryStepDamage(Entity<SurgeryTargetComponent> ent, ref SurgeryStepDamageEvent args) =>
        SetDamage(args.Body, args.Damage, args.PartMultiplier, args.User, args.Part);

    private void OnSurgeryDamageChange(Entity<SurgeryDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        var damageChange = ent.Comp.Damage;
        if (Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(args.Body))
            damageChange = damageChange * ent.Comp.SleepModifier;

        SetDamage(args.Body, damageChange, 0.5f, args.User, args.Part, ent.Comp.AffectAll);
    }
    private void OnSurgerySpecialDamageChange(Entity<SurgerySpecialDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        if (!TryComp<DamageableComponent>(args.Body, out var damageable))
            return;

        if (!damageable.Damage.DamageDict.TryGetValue(ent.Comp.DamageType, out var current) || current <= 0)
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict[ent.Comp.DamageType] = -current;
        _damageable.TryChangeDamage(args.Body, heal, true, origin: args.User);
    }

    private void OnSurgeryDamageUser(Entity<SurgeryDamageUserComponent> ent, ref SurgeryStepEvent args)
    {
        _damageable.TryChangeDamage(args.User, ent.Comp.Damage);
        if (ent.Comp.Popup is { } popup)
        {
            var msg = Loc.GetString(popup, ("target", args.Body), ("part", args.Part));
            _popup.PopupEntity(msg, args.Body, args.User, PopupType.SmallCaution);
        }
    }

    private void OnStepScreamComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(args.Body))
            return;

        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }
    private void OnStepSpawnComplete(Entity<SurgeryStepSpawnEffectComponent> ent, ref SurgeryStepEvent args) =>
        SpawnAtPosition(ent.Comp.Entity, Transform(args.Body).Coordinates);
}
