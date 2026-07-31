using System.Numerics;
using Content.Shared._FinalStand.Deployables;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Deployables;

// Drives the Damage Beacon's floor-effect animation off Update() polling every tick, deriving state from the server-stamped SpawnedAt timestamp.
public sealed class FSDamageBeaconFieldVfxSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float ActivateDuration = 0.08f * 7f;
    private const float PulseSpeed = 2f;
    private const float PulseAmount = 0.03f;
    private const float SpinRadiansPerSecond = 0.35f;

    private readonly HashSet<EntityUid> _started = new();
    private readonly HashSet<EntityUid> _settled = new();
    private float _time;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDamageBeaconFieldVfxComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(EntityUid uid, FSDamageBeaconFieldVfxComponent comp, ref EntityTerminatingEvent args)
    {
        _started.Remove(uid);
        _settled.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _time += frameTime;
        var pulse = 1f + PulseAmount * MathF.Sin(_time * PulseSpeed);
        var spin = new Angle(SpinRadiansPerSecond * _time);
        var query = EntityQueryEnumerator<FSDamageBeaconFieldVfxComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            sprite[0].Rotation = spin;

            if (_settled.Contains(uid))
            {
                sprite[0].Scale = new Vector2(pulse, pulse);
                continue;
            }

            var elapsed = (float) (_timing.CurTime - comp.SpawnedAt).TotalSeconds;

            if (elapsed >= ActivateDuration)
            {
                _settled.Add(uid);
                _started.Remove(uid);
                sprite[0].AutoAnimated = false;
                _sprite.LayerSetRsiState((uid, sprite), 0, "loop");
                sprite[0].Scale = new Vector2(pulse, pulse);
            }
            else if (_started.Add(uid))
            {
                sprite[0].AutoAnimated = true;
                _sprite.LayerSetRsiState((uid, sprite), 0, "activate");
            }
        }
    }
}
