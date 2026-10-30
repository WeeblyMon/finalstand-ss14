// Effects that follow a state rather than a moment: a hurt mech smokes and sparks, a mech out of power flickers and spits sparks,
// a walking mech kicks up dust, uranium glows,
// and a fortification chips when it takes a hit.
using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.Mech;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Damage.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared._FinalStand.Particles;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Effects;

public sealed partial class FSLingeringFlairSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private FSFlairClientSystem _flair = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> Debris = "FSDebris";
    private static readonly ProtoId<FSParticleEmitterPrototype> PowerSpark = "FSWeldSparks";
    private static readonly ProtoId<FSParticleEmitterPrototype> DustPuff = "FSDustPuff";
    private static readonly ProtoId<FSParticleEmitterPrototype> MechSmoke = "FSMechSmoke";
    private static readonly ProtoId<FSParticleEmitterPrototype> MechSparks = "FSMechSparks";
    private static readonly ProtoId<FSParticleEmitterPrototype> UraniumGlow = "FSUraniumGlow";

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
        public bool Dimmed;
        public double NextSpark;
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
                    Smoke = _particles.Attach(MechSmoke, uid),
                    Sparks = _particles.Attach(MechSparks, uid),
                    LastStep = _transform.GetWorldPosition(xform),
                };
                _mechs[uid] = flair;
            }

            var health = mech.MaxIntegrity > 0 ? (mech.Integrity / mech.MaxIntegrity).Float() : 1f;
            var alive = !mech.Broken;
            _particles.SetRateScale(flair.Smoke, alive && health < SmokeBelow ? 1f : 0f);
            _particles.SetRateScale(flair.Sparks, alive && health < SparksBelow ? 1f : 0f);
            Brownout(uid, flair, alive && CompOrNull<FSMechLowPowerComponent>(uid)?.Active == true);

            var pos = _transform.GetWorldPosition(xform);
            if ((pos - flair.LastStep).Length() < StepLength)
                continue;

            flair.LastStep = pos;
            _particles.Burst(DustPuff, uid, countScale: 0.4f);
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

    private void Brownout(EntityUid uid, MechFlair flair, bool low)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!low)
        {
            if (flair.Dimmed)
                _sprite.SetColor((uid, sprite), Color.White);
            flair.Dimmed = false;
            return;
        }

        flair.Dimmed = true;
        var t = _timing.RealTime.TotalSeconds;
        var stutter = (int) (t * 9) * 7919 % 13 == 0;
        var level = stutter ? 0.35f : 0.68f + 0.12f * MathF.Sin((float) t * 3f);
        _sprite.SetColor((uid, sprite), new Color(level, level, level * 1.05f));

        if (t < flair.NextSpark)
            return;

        flair.NextSpark = t + 0.7 + (int) (t * 13) % 9 * 0.1;
        _particles.Burst(PowerSpark, uid, countScale: 0.4f);
    }

    private void UpdateDebris()
    {
        var query = EntityQueryEnumerator<FSStructureHealthComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out _, out var damageable))
        {
            var total = damageable.TotalDamage.Float();
            if (_damage.TryGetValue(uid, out var last) && total > last + 0.5f)
                _particles.Burst(Debris, uid, countScale: 0.3f);

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
                _glows[uid] = _particles.Attach(UraniumGlow, uid);
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
