using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using System.Collections.Frozen;
using System.Linq;

namespace Content.Shared.NPC.Systems;

/// <summary>
///     Outlines faction relationships with each other.
/// </summary>
public sealed partial class NpcFactionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    /// <summary>
    /// To avoid prototype mutability we store an intermediary data class that gets used instead.
    /// </summary>
    private FrozenDictionary<string, FactionData> _factions = FrozenDictionary<string, FactionData>.Empty;

    // FINALSTAND: members per faction, so hostile scans can skip the range lookup when the wanted factions are small.
    private readonly Dictionary<ProtoId<NpcFactionPrototype>, HashSet<EntityUid>> _members = new();
    private const int DirectScanLimit = 128;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcFactionMemberComponent, ComponentStartup>(OnFactionStartup);
        SubscribeLocalEvent<NpcFactionMemberComponent, ComponentShutdown>(OnFactionShutdown);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);

        InitializeException();
        RefreshFactions();
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<NpcFactionPrototype>())
            RefreshFactions();
    }

    private void OnFactionStartup(Entity<NpcFactionMemberComponent> ent, ref ComponentStartup args)
    {
        foreach (var faction in ent.Comp.Factions)
            AddMember(ent, faction);

        RefreshFactions(ent);
    }

    private void OnFactionShutdown(Entity<NpcFactionMemberComponent> ent, ref ComponentShutdown args)
    {
        foreach (var faction in ent.Comp.Factions)
            RemoveMember(ent, faction);
    }

    private void AddMember(EntityUid uid, ProtoId<NpcFactionPrototype> faction)
    {
        if (!_members.TryGetValue(faction, out var members))
        {
            members = new HashSet<EntityUid>();
            _members[faction] = members;
        }

        members.Add(uid);
    }

    private void RemoveMember(EntityUid uid, ProtoId<NpcFactionPrototype> faction)
    {
        if (_members.TryGetValue(faction, out var members))
            members.Remove(uid);
    }

    /// <summary>
    /// Refreshes the cached factions for this component.
    /// </summary>
    private void RefreshFactions(Entity<NpcFactionMemberComponent> ent)
    {
        ent.Comp.FriendlyFactions.Clear();
        ent.Comp.HostileFactions.Clear();

        foreach (var faction in ent.Comp.Factions)
        {
            // YAML Linter already yells about this, don't need to log an error here
            if (!_factions.TryGetValue(faction, out var factionData))
                continue;

            ent.Comp.FriendlyFactions.UnionWith(factionData.Friendly);
            ent.Comp.HostileFactions.UnionWith(factionData.Hostile);
        }
        // Add additional factions if it is written in prototype
        if (ent.Comp.AddFriendlyFactions != null)
        {
            ent.Comp.FriendlyFactions.UnionWith(ent.Comp.AddFriendlyFactions);
        }
        if (ent.Comp.AddHostileFactions != null)
        {
            ent.Comp.HostileFactions.UnionWith(ent.Comp.AddHostileFactions);
        }
    }

    /// <summary>
    /// Returns whether an entity is a member of a faction.
    /// </summary>
    public bool IsMember(Entity<NpcFactionMemberComponent?> ent, [ForbidLiteral] string faction)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        return ent.Comp.Factions.Contains(faction);
    }

    /// <summary>
    /// Returns whether an entity is a member of any listed faction.
    /// If the list is empty this returns false.
    /// </summary>
    public bool IsMemberOfAny(Entity<NpcFactionMemberComponent?> ent, [ForbidLiteral] IEnumerable<ProtoId<NpcFactionPrototype>> factions)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        foreach (var faction in factions)
        {
            if (ent.Comp.Factions.Contains(faction))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds this entity to the particular faction.
    /// </summary>
    public void AddFaction(Entity<NpcFactionMemberComponent?> ent, [ForbidLiteral] string faction, bool dirty = true)
    {
        if (!ProtoMan.HasIndex<NpcFactionPrototype>(faction))
        {
            Log.Error($"Unable to find faction {faction}");
            return;
        }

        ent.Comp ??= EnsureComp<NpcFactionMemberComponent>(ent);
        if (!ent.Comp.Factions.Add(faction))
            return;

        AddMember(ent, faction);

        if (dirty)
            RefreshFactions((ent, ent.Comp));
    }

    /// <summary>
    /// Adds this entity to the particular faction.
    /// </summary>
    public void AddFactions(Entity<NpcFactionMemberComponent?> ent, [ForbidLiteral] HashSet<ProtoId<NpcFactionPrototype>> factions, bool dirty = true)
    {
        ent.Comp ??= EnsureComp<NpcFactionMemberComponent>(ent);

        foreach (var faction in factions)
        {
            if (!ProtoMan.HasIndex(faction))
            {
                Log.Error($"Unable to find faction {faction}");
                continue;
            }

            if (ent.Comp.Factions.Add(faction))
                AddMember(ent, faction);
        }

        if (dirty)
            RefreshFactions((ent, ent.Comp));
    }

    /// <summary>
    /// Removes this entity from the particular faction.
    /// </summary>
    public void RemoveFaction(Entity<NpcFactionMemberComponent?> ent, [ForbidLiteral] string faction, bool dirty = true)
    {
        if (!ProtoMan.HasIndex<NpcFactionPrototype>(faction))
        {
            Log.Error($"Unable to find faction {faction}");
            return;
        }

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!ent.Comp.Factions.Remove(faction))
            return;

        RemoveMember(ent, faction);

        if (dirty)
            RefreshFactions((ent, ent.Comp));
    }

    /// <summary>
    /// Remove this entity from all factions.
    /// </summary>
    public void ClearFactions(Entity<NpcFactionMemberComponent?> ent, bool dirty = true)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var faction in ent.Comp.Factions)
            RemoveMember(ent, faction);

        ent.Comp.Factions.Clear();

        if (dirty)
            RefreshFactions((ent, ent.Comp));
    }

    public IEnumerable<EntityUid> GetNearbyHostiles(Entity<NpcFactionMemberComponent?, FactionExceptionComponent?> ent, float range)
    {
        if (!Resolve(ent, ref ent.Comp1, false))
            return Array.Empty<EntityUid>();

        var hostiles = GetNearbyFactions(ent, range, ent.Comp1.HostileFactions)
            // ignore mobs that have both hostile faction and the same faction,
            // otherwise having multiple factions is strictly negative
            .Where(target => !IsEntityFriendly((ent, ent.Comp1), target));
        if (!Resolve(ent, ref ent.Comp2, false))
            return hostiles;

        // ignore anything from enemy faction that we are explicitly friendly towards
        var faction = (ent.Owner, ent.Comp2);
        return hostiles
            .Union(GetHostiles(faction))
            .Where(target => !IsIgnored(faction, target));
    }

    public IEnumerable<EntityUid> GetNearbyFriendlies(Entity<NpcFactionMemberComponent?> ent, float range)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return Array.Empty<EntityUid>();

        return GetNearbyFactions(ent, range, ent.Comp.FriendlyFactions);
    }

    private IEnumerable<EntityUid> GetNearbyFactions(EntityUid entity, float range, [ForbidLiteral] HashSet<ProtoId<NpcFactionPrototype>> factions)
    {
        var xform = Transform(entity);
        var coords = _xform.GetMapCoordinates((entity, xform));

        // FINALSTAND: every NPC scans for hostiles, and in a horde a range lookup mostly returns its own side.
        // When the wanted factions are small, checking their members directly is far cheaper.
        var candidates = 0;
        foreach (var faction in factions)
        {
            if (_members.TryGetValue(faction, out var members))
                candidates += members.Count;
        }

        if (candidates <= DirectScanLimit)
        {
            var rangeSquared = range * range;
            foreach (var faction in factions)
            {
                if (!_members.TryGetValue(faction, out var members))
                    continue;

                foreach (var uid in members)
                {
                    if (uid == entity
                        || !TryComp<NpcFactionMemberComponent>(uid, out var member)
                        || FirstSharedFaction(factions, member) != faction)
                        continue;

                    var memberXform = Transform(uid);
                    if (memberXform.MapID != coords.MapId
                        || (MetaData(uid).Flags & MetaDataFlags.InContainer) != 0
                        || (_xform.GetWorldPosition(memberXform) - coords.Position).LengthSquared() > rangeSquared)
                        continue;

                    yield return uid;
                }
            }

            yield break;
        }

        foreach (var ent in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(coords, range))
        {
            if (ent.Owner == entity)
                continue;

            if (!factions.Overlaps(ent.Comp.Factions))
                continue;

            yield return ent.Owner;
        }
    }

    // An entity in several of the wanted factions is only returned under the first one.
    private static ProtoId<NpcFactionPrototype>? FirstSharedFaction(HashSet<ProtoId<NpcFactionPrototype>> factions, NpcFactionMemberComponent member)
    {
        foreach (var faction in factions)
        {
            if (member.Factions.Contains(faction))
                return faction;
        }

        return null;
    }

    /// <remarks>
    /// 1-way and purely faction based, ignores faction exception.
    /// </remarks>
    public bool IsEntityFriendly(Entity<NpcFactionMemberComponent?> ent, Entity<NpcFactionMemberComponent?> other)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !Resolve(other, ref other.Comp, false))
            return false;

        return ent.Comp.Factions.Overlaps(other.Comp.Factions) || ent.Comp.FriendlyFactions.Overlaps(other.Comp.Factions);
    }

    public bool IsFactionFriendly([ForbidLiteral] string target, [ForbidLiteral] string with)
    {
        return _factions[target].Friendly.Contains(with) && _factions[with].Friendly.Contains(target);
    }

    public bool IsFactionFriendly([ForbidLiteral] string target, Entity<NpcFactionMemberComponent?> with)
    {
        if (!Resolve(with, ref with.Comp, false))
            return false;

        return with.Comp.Factions.All(x => IsFactionFriendly(target, x)) ||
               with.Comp.FriendlyFactions.Contains(target);
    }

    public bool IsFactionHostile([ForbidLiteral] string target, [ForbidLiteral] string with)
    {
        return _factions[target].Hostile.Contains(with) && _factions[with].Hostile.Contains(target);
    }

    public bool IsFactionHostile([ForbidLiteral] string target, Entity<NpcFactionMemberComponent?> with)
    {
        if (!Resolve(with, ref with.Comp, false))
            return false;

        return with.Comp.Factions.All(x => IsFactionHostile(target, x)) ||
               with.Comp.HostileFactions.Contains(target);
    }

    public bool IsFactionNeutral([ForbidLiteral] string target, [ForbidLiteral] string with)
    {
        return !IsFactionFriendly(target, with) && !IsFactionHostile(target, with);
    }

    /// <summary>
    /// Makes the source faction friendly to the target faction, 1-way.
    /// </summary>
    public void MakeFriendly([ForbidLiteral] string source, [ForbidLiteral] string target)
    {
        if (!_factions.TryGetValue(source, out var sourceFaction))
        {
            Log.Error($"Unable to find faction {source}");
            return;
        }

        if (!_factions.ContainsKey(target))
        {
            Log.Error($"Unable to find faction {target}");
            return;
        }

        sourceFaction.Friendly.Add(target);
        sourceFaction.Hostile.Remove(target);
        RefreshFactions();
    }

    /// <summary>
    /// Makes the source faction hostile to the target faction, 1-way.
    /// </summary>
    public void MakeHostile([ForbidLiteral] string source, [ForbidLiteral] string target)
    {
        if (!_factions.TryGetValue(source, out var sourceFaction))
        {
            Log.Error($"Unable to find faction {source}");
            return;
        }

        if (!_factions.ContainsKey(target))
        {
            Log.Error($"Unable to find faction {target}");
            return;
        }

        sourceFaction.Friendly.Remove(target);
        sourceFaction.Hostile.Add(target);
        RefreshFactions();
    }

    private void RefreshFactions()
    {
        _factions = ProtoMan.EnumeratePrototypes<NpcFactionPrototype>().ToFrozenDictionary(
            faction => faction.ID,
            faction =>  new FactionData
            {
                Friendly = faction.Friendly.ToHashSet(),
                Hostile = faction.Hostile.ToHashSet()
            });

        var query = AllEntityQuery<NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.FriendlyFactions.Clear();
            comp.HostileFactions.Clear();
            RefreshFactions((uid, comp));
        }
    }
}
