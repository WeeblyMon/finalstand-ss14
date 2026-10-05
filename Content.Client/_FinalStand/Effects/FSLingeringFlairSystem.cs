// Effects that follow a state rather than a moment: a hurt mech smokes and sparks, a walking mech kicks up dust, uranium glows,
// and a fortification chips when it takes a hit.
using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.Mech;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Effects;

public sealed partial class FSLingeringFlairSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private FSFlairClientSystem _flair = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<FSUpgradePrototype> Uranium = "FSGradeUranium";
    private const float SmokeBelow = 0.25f;
    private const float SparksBelow = 0.5f;
    private const float StepLength = 0.9f;
    private const float StepKickRange = 3f;
    private const float StepKick = 0.15f;
    private const float GlowScanInterval = 0.5f;
    private const float DebrisInterval = 0.1f;

    private readonly Dictionary<EntityUid, MechFlair> _mechs = new();
    private readonly Dictionary<EntityUid, int> _glows = new();
    private readonly Dictionary<EntityUid, float> _damage = new();
    private readonly List<EntityUid> _dead = new();
    private float _glowScan;
    private float _debrisScan;

    private sealed class MechFlair
    {
        public int Smoke;
        public int Sparks;
        public System.Numerics.Vector2 LastStep;
    }

    public override void FrameUpdate(float frameTime)
    {
        UpdateMechs();

        _debrisScan -= frameTime;
        if (_debrisScan <= 0f)
        {
            _debrisScan = DebrisInterval;
            UpdateDebris();
        }

        _glowScan -= frameTime;
        if (_glowScan > 0f)
            return;

        _glowScan = GlowScanInterval;
        UpdateGlows();
    }

    private void UpdateMechs()
    {
        EntityUid? piloting = null;
        if (_player.LocalEntity is { } local && TryComp<VehicleOperatorComponent>(local, out var op))
            piloting = op.Vehicle;

        var query = EntityQueryEnumerator<FSCombatMechComponent, MechComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mech, out var xform))
        {
            if (!_mechs.TryGetValue(uid, out var flair))
            {
                flair = new MechFlair
                {
                    Smoke = _particles.Attach("FSMechSmoke", uid),
                    Sparks = _particles.Attach("FSMechSparks", uid),
                    LastStep = _transform.GetWorldPosition(xform),
                };
                _mechs[uid] = flair;
            }

            var health = mech.MaxIntegrity > 0 ? (mech.Integrity / mech.MaxIntegrity).Float() : 1f;
            var alive = !mech.Broken;
            _particles.SetRateScale(flair.Smoke, alive && health < SmokeBelow ? 1f : 0f);
            _particles.SetRateScale(flair.Sparks, alive && health < SparksBelow ? 1f : 0f);

            var pos = _transform.GetWorldPosition(xform);
            if ((pos - flair.LastStep).Length() < StepLength)
                continue;

            flair.LastStep = pos;
            _particles.Burst("FSDustPuff", uid, countScale: 0.4f);
            if (piloting != uid)
                _flair.Kick(_transform.GetMapCoordinates(uid, xform), StepKick, StepKickRange);
        }

        foreach (var uid in _mechs.Keys)
        {
            if (!TerminatingOrDeleted(uid))
                continue;

            _dead.Add(uid);
        }

        foreach (var uid in _dead)
        {
            _particles.Detach(_mechs[uid].Smoke);
            _particles.Detach(_mechs[uid].Sparks);
            _mechs.Remove(uid);
        }

        _dead.Clear();
    }

    private void UpdateDebris()
    {
        var query = EntityQueryEnumerator<FSStructureHealthComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out _, out var damageable))
        {
            var total = damageable.TotalDamage.Float();
            if (_damage.TryGetValue(uid, out var last) && total > last + 0.5f)
                _particles.Burst("FSDebris", uid, countScale: 0.3f);

            _damage[uid] = total;
        }

        foreach (var uid in _damage.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _dead.Add(uid);
        }

        foreach (var uid in _dead)
            _damage.Remove(uid);

        _dead.Clear();
    }

    private void UpdateGlows()
    {
        var query = EntityQueryEnumerator<FSUpgradeableComponent>();
        while (query.MoveNext(out var uid, out var upgradeable))
        {
            var uranium = upgradeable.Grade == Uranium;
            var has = _glows.ContainsKey(uid);
            if (uranium && !has)
                _glows[uid] = _particles.Attach("FSUraniumGlow", uid);
            else if (!uranium && has)
            {
                _particles.Detach(_glows[uid]);
                _glows.Remove(uid);
            }
        }

        foreach (var uid in _glows.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _dead.Add(uid);
        }

        foreach (var uid in _dead)
            _glows.Remove(uid);

        _dead.Clear();
    }
}
