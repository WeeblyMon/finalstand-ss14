using System.Numerics;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantFlashlightSystem : EntitySystem
{
    [Dependency] private FSStunOverrideSystem _stunOverride = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSRevenantEffectSystem _effects = default!;
    [Dependency] private FSRevenantSystem _revenant = default!;

    private const float StunRadius = 3.5f;
    private const float PollInterval = 0.15f;

    private static readonly float[] StunDurations = { 1.2f, 0.8f, 0.5f };
    private const float AdaptWindow = 12f;
    private const float AdaptedImmunity = 10f;

    private float _accum;

    private readonly HashSet<Entity<ActorComponent>> _playerBuffer = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accum += frameTime;
        if (_accum < PollInterval)
            return;
        _accum = 0f;

        if (!_revenant.IsDarkWave)
            return;

        var now = _timing.CurTime;

        var revenantQuery = EntityQueryEnumerator<FSRevenantComponent, MobStateComponent>();
        while (revenantQuery.MoveNext(out var revUid, out var rev, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (now < rev.LightStunImmuneUntil)
                continue;

            if (TryComp<FSForceStunComponent>(revUid, out var forced) && now < forced.ExpiresAt)
                continue;

            var revPos = _transform.GetMapCoordinates(revUid);
            if (revPos.MapId == MapId.Nullspace)
                continue;

            _playerBuffer.Clear();
            _lookup.GetEntitiesInRange<ActorComponent>(revPos, StunRadius, _playerBuffer);

            foreach (var (playerUid, _) in _playerBuffer)
            {
                if (HasComp<WaveSpawnedTagComponent>(playerUid)) continue;
                if (HasComp<GhostComponent>(playerUid)) continue;
                if (TryComp<MobStateComponent>(playerUid, out var pms) && pms.CurrentState != MobState.Alive) continue;
                var hasActiveLight = false;
                foreach (var carried in _inventory.GetHandOrInventoryEntities(playerUid))
                {
                    if (TryComp<HandheldLightComponent>(carried, out var light) && light.Activated)
                    {
                        hasActiveLight = true;
                        break;
                    }
                }
                if (!hasActiveLight) continue;

                if (_transform.GetMapCoordinates(playerUid).MapId != revPos.MapId) continue;

                if (!_examine.InRangeUnOccluded(playerUid, revUid, StunRadius, null)) continue;

                if (now >= rev.LightStunWindowEnd)
                    rev.LightStunCount = 0;

                if (rev.LightStunCount >= StunDurations.Length)
                {
                    rev.LightStunImmuneUntil = now + TimeSpan.FromSeconds(AdaptedImmunity);
                    rev.LightStunCount = 0;
                    break;
                }

                var duration = TimeSpan.FromSeconds(StunDurations[rev.LightStunCount]);
                if (!_stunOverride.TryForceStun(revUid, duration, visualized: true))
                    break;

                rev.LightStunCount++;
                rev.LightStunWindowEnd = now + TimeSpan.FromSeconds(AdaptWindow);
                _revenant.ForceRetreat(revUid);
                break;
            }
        }
    }
}
