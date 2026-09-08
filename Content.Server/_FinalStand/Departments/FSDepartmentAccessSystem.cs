using Content.Shared._FinalStand.Departments;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Departments;

// Single source of truth for department membership; keeps FSDepartmentAccessComponent in step with the player's ID.
public sealed partial class FSDepartmentAccessSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    private static readonly ProtoId<DepartmentPrototype> ScienceDept = "Science";
    private static readonly ProtoId<DepartmentPrototype> EngineeringDept = "Engineering";

    private static readonly ProtoId<AccessLevelPrototype>[] ScienceAccess = ["Research", "ResearchDirector"];
    private static readonly ProtoId<AccessLevelPrototype>[] EngineeringAccess = ["Engineering", "ChiefEngineer"];

    // An ID console can rewrite a card that never leaves the player, which fires nothing we can subscribe to.
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    private TimeSpan _nextSweep;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<FSDepartmentAccessComponent, DidEquipEvent>(OnEquipped);
        SubscribeLocalEvent<FSDepartmentAccessComponent, DidUnequipEvent>(OnUnequipped);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        Refresh(ev.Mob);
    }

    private void OnEquipped(Entity<FSDepartmentAccessComponent> ent, ref DidEquipEvent args)
    {
        Refresh(ent.Owner);
    }

    private void OnUnequipped(Entity<FSDepartmentAccessComponent> ent, ref DidUnequipEvent args)
    {
        Refresh(ent.Owner);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _nextSweep = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextSweep)
            return;

        _nextSweep = _timing.CurTime + SweepInterval;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob)
                Refresh(mob);
        }
    }

    public bool IsScience(EntityUid user) => IsInDepartment(user, ScienceDept, ScienceAccess);

    public bool IsEngineering(EntityUid user) => IsInDepartment(user, EngineeringDept, EngineeringAccess);

    public void Refresh(EntityUid user)
    {
        if (TerminatingOrDeleted(user))
            return;

        var comp = EnsureComp<FSDepartmentAccessComponent>(user);
        var science = IsScience(user);
        var engineering = IsEngineering(user);

        if (comp.Science == science && comp.Engineering == engineering)
            return;

        comp.Science = science;
        comp.Engineering = engineering;
        Dirty(user, comp);
    }

    private bool IsInDepartment(EntityUid user, ProtoId<DepartmentPrototype> department,
        ProtoId<AccessLevelPrototype>[] accessLevels)
    {
        return HasAccess(user, accessLevels) || HasJobIn(user, department);
    }

    // Promotions hand out a departmental ID without changing the mind's job, so access has to count too.
    private bool HasAccess(EntityUid user, ProtoId<AccessLevelPrototype>[] accessLevels)
    {
        var tags = _accessReader.FindAccessTags(user);
        foreach (var access in accessLevels)
        {
            if (tags.Contains(access))
                return true;
        }

        return false;
    }

    private bool HasJobIn(EntityUid user, ProtoId<DepartmentPrototype> department)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;

        if (!_roles.MindHasRole(mindId, typeof(JobRoleComponent), out var jobRole))
            return false;

        if (jobRole.Value.Comp.JobPrototype is not { } jobProtoId)
            return false;

        return _proto.TryIndex(department, out var dept) && dept.Roles.Contains(jobProtoId);
    }
}
