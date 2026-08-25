using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantLightDrainSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float DrainRadius = 5f;
    private const float PollInterval = 0.4f;

    private float _accum;

    private readonly HashSet<Entity<PoweredLightComponent>> _lightBuffer = new();
    private readonly List<EntityUid> _restoreBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(FSRevenantSystem));
        SubscribeLocalEvent<FSRevenantLightDrainComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, FSRevenantLightDrainComponent comp, ComponentShutdown args)
    {
        RestoreAll(comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accum += frameTime;
        if (_accum < PollInterval)
            return;
        _accum = 0f;

        if (_waveRule.IsDarkWaveActive())
        {
            var stale = EntityQueryEnumerator<FSRevenantLightDrainComponent>();
            while (stale.MoveNext(out _, out var staleComp))
                staleComp.Drained.Clear();
            return;
        }

        var query = EntityQueryEnumerator<FSRevenantComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
            {
                if (TryComp<FSRevenantLightDrainComponent>(uid, out var deadDrain))
                    RestoreAll(deadDrain);
                continue;
            }

            var drain = EnsureComp<FSRevenantLightDrainComponent>(uid);

            var pos = _transform.GetMapCoordinates(uid);
            if (pos.MapId == MapId.Nullspace)
            {
                RestoreAll(drain);
                continue;
            }

            _lightBuffer.Clear();
            _lookup.GetEntitiesInRange(pos, DrainRadius, _lightBuffer);

            RestoreOutOfRange(drain, pos);

            foreach (var (lightUid, light) in _lightBuffer)
            {
                if (!light.On || drain.Drained.Contains(lightUid))
                    continue;

                _poweredLight.SetState(lightUid, false, light);
                drain.Drained.Add(lightUid);
            }
        }
    }

    private void RestoreOutOfRange(FSRevenantLightDrainComponent comp, MapCoordinates pos)
    {
        _restoreBuffer.Clear();

        foreach (var lightUid in comp.Drained)
        {
            if (TerminatingOrDeleted(lightUid))
            {
                _restoreBuffer.Add(lightUid);
                continue;
            }

            var lightPos = _transform.GetMapCoordinates(lightUid);
            if (lightPos.MapId == pos.MapId
                && (lightPos.Position - pos.Position).Length() <= DrainRadius)
                continue;

            _restoreBuffer.Add(lightUid);
            Restore(lightUid);
        }

        foreach (var lightUid in _restoreBuffer)
            comp.Drained.Remove(lightUid);
    }

    private void RestoreAll(FSRevenantLightDrainComponent comp)
    {
        foreach (var lightUid in comp.Drained)
            Restore(lightUid);

        comp.Drained.Clear();
    }

    private void Restore(EntityUid lightUid)
    {
        if (TryComp<PoweredLightComponent>(lightUid, out var light))
            _poweredLight.SetState(lightUid, true, light);
    }

}
