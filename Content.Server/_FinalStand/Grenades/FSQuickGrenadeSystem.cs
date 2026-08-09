using Content.Server.Popups;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.Explosion.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Grenades;

public sealed class FSQuickGrenadeSystem : EntitySystem
{
    [Dependency] private readonly FSGrenadeSelectActionSystem _grenadeSelect = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly ProtoId<TagPrototype> HandGrenadeTag = "HandGrenade";

    // Matches baitDuration on FSGrenadePackPipe. Upgrades add to that field, so the
    // singularity needs the unupgraded value to recover how much was added.
    private const float PipeBaseBaitDuration = 8f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSQuickGrenadeMessage>(OnQuickGrenade);
    }

    private void OnQuickGrenade(FSQuickGrenadeMessage msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;
        if (user == null)
            return;

        if (_mobState.IsIncapacitated(user.Value))
            return;

        // Prefer grenade pack system if the player has bought any type.
        if (TryComp<FSActiveGrenadeComponent>(user.Value, out var active))
        {
            var pack = FindGrenadePack(user.Value, active.ActiveType);
            if (pack != null)
            {
                if (!TryComp<FSGrenadePackComponent>(pack.Value, out var packComp) || packComp.Stock <= 0)
                {
                    _popup.PopupEntity(Loc.GetString("grenade-pack-empty", ("type", active.ActiveType.ToString())), user.Value, user.Value);
                    return;
                }

                packComp.Stock--;
                Dirty(pack.Value, packComp);
                _grenadeSelect.SyncPackCounter(pack.Value, packComp);

                var coords = Transform(user.Value).Coordinates;
                var protoToSpawn = packComp.IsSingularity
                    ? (EntProtoId)"FSGrenadeSingularity"
                    : packComp.IsCluster
                        ? (EntProtoId)"FSGrenadeFragCluster"
                        : packComp.GrenadeProtoId;
                var grenade = Spawn(protoToSpawn, coords);

                // Transfer pack-level upgrade data to the spawned grenade.
                if (TryComp<FSFireZoneOnTriggerComponent>(grenade, out var fireZone))
                {
                    fireZone.BurnDuration = packComp.BurnDuration;
                    fireZone.EffectRadius = packComp.EffectRadius;
                    fireZone.DamageMultiplier = 1f + packComp.BlastBonus;
                }
                if (TryComp<FSStunInRadiusOnTriggerComponent>(grenade, out var stun))
                {
                    stun.StunDuration = TimeSpan.FromSeconds(packComp.StunDuration);
                    stun.Radius += packComp.EffectRadius;
                }
                if (TryComp<FSBaitOnTriggerComponent>(grenade, out var bait))
                    bait.BaitDuration = packComp.BaitDuration;
                if (TryComp<FSSingularityOnTriggerComponent>(grenade, out var singularity))
                {
                    singularity.ExtraRadius = packComp.EffectRadius;
                    singularity.ExtraDuration = packComp.BaitDuration - PipeBaseBaitDuration;
                    singularity.DamageMultiplier = 1f + packComp.BlastBonus;
                }
                if (TryComp<ExplosiveComponent>(grenade, out var expComp) && packComp.BlastBonus > 0f)
                {
#pragma warning disable RA0002
                    expComp.TotalIntensity *= 1f + packComp.BlastBonus;
#pragma warning restore RA0002
                }

                if (packComp.ImpactFuse)
                {
                    var land = EnsureComp<TriggerOnLandComponent>(grenade);
                    land.KeyOut = "timer";
                    var collide = EnsureComp<TriggerOnCollideComponent>(grenade);
                    collide.FixtureID = "fix1";
                    collide.KeyOut = "timer";
                    ThrowGrenade(grenade, user.Value, msg.CursorWorldPos, arm: false);
                }
                else
                {
                    ThrowGrenade(grenade, user.Value, msg.CursorWorldPos);
                }
                return;
            }
        }

        // Fallback: look for a physical grenade with the HandGrenade tag.
        var fallback = FindGrenadeInInventory(user.Value);
        if (fallback == null)
        {
            _popup.PopupEntity("No grenade found.", user.Value, user.Value);
            return;
        }

        // Yank directly out of whatever container it's in (pocket, bag, belt) — no hand needed.
        if (_containers.TryGetContainingContainer(fallback.Value, out var container))
            _containers.Remove(fallback.Value, container, destination: Transform(user.Value).Coordinates);

        ThrowGrenade(fallback.Value, user.Value, msg.CursorWorldPos);
    }

    private void ThrowGrenade(EntityUid grenade, EntityUid user, System.Numerics.Vector2 cursorWorldPos, bool arm = true)
    {
        // Persist thrower so chain-reaction explosions keep the correct owner for friendly-fire attribution.
        EnsureComp<FSGrenadeOwnerComponent>(grenade).Thrower = user;

        if (arm)
            RaiseLocalEvent(grenade, new UseInHandEvent(user));

        // Throw toward cursor. Fall back to facing direction if cursor is on top of player.
        var playerWorldPos = _transform.GetWorldPosition(user);
        var dir = cursorWorldPos - playerWorldPos;
        if (dir.LengthSquared() < 0.01f)
            dir = _transform.GetWorldRotation(user).ToWorldVec();

        _throwing.TryThrow(grenade, dir, 10f, user);
    }

    private EntityUid? FindGrenadePack(EntityUid user, GrenadeType type)
    {
        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        foreach (var cont in mgr.Containers.Values)
        {
            foreach (var item in cont.ContainedEntities)
            {
                if (TryComp<FSGrenadePackComponent>(item, out var pack) && pack.PackType == type)
                    return item;

                if (!TryComp<ContainerManagerComponent>(item, out var innerMgr))
                    continue;
                foreach (var inner in innerMgr.Containers.Values)
                {
                    foreach (var innerItem in inner.ContainedEntities)
                    {
                        if (TryComp<FSGrenadePackComponent>(innerItem, out var innerPack) && innerPack.PackType == type)
                            return innerItem;
                    }
                }
            }
        }

        return null;
    }

    private EntityUid? FindGrenadeInInventory(EntityUid user)
    {
        // Check active hand first
        var active = _hands.GetActiveItem(user);
        if (active != null && _tags.HasTag(active.Value, HandGrenadeTag))
            return active.Value;

        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (_tags.HasTag(item, HandGrenadeTag))
                    return item;

                // One level deep (belt, pockets, backpack)
                if (!TryComp<ContainerManagerComponent>(item, out var innerMgr))
                    continue;

                foreach (var inner in innerMgr.Containers.Values)
                {
                    foreach (var innerItem in inner.ContainedEntities)
                    {
                        if (_tags.HasTag(innerItem, HandGrenadeTag))
                            return innerItem;
                    }
                }
            }
        }

        return null;
    }
}
