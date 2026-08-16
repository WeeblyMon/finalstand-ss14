using System.Numerics;
using Content.Shared._FinalStand.Deployables;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Deployables;

public sealed class FSDamageBeaconFieldVfxSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float ActivateDuration = 0.08f * 7f;
    private const float PulseSpeed = 2f;
    private const float PulseAmount = 0.03f;
    private const float SpinRadiansPerSecond = 0.35f;

    private float _time;

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

            if (comp.Settled)
            {
                sprite[0].Scale = new Vector2(pulse, pulse);
                continue;
            }

            if ((float) (_timing.CurTime - comp.SpawnedAt).TotalSeconds >= ActivateDuration)
            {
                comp.Settled = true;
                sprite[0].AutoAnimated = false;
                _sprite.LayerSetRsiState((uid, sprite), 0, "loop");
                sprite[0].Scale = new Vector2(pulse, pulse);
                continue;
            }

            if (comp.IntroStarted)
                continue;

            comp.IntroStarted = true;
            sprite[0].AutoAnimated = true;
            _sprite.LayerSetRsiState((uid, sprite), 0, "activate");
        }
    }
}
