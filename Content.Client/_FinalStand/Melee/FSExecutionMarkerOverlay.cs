using System.Numerics;
using Content.Client.Weapons.Melee;
using Content.Shared._FinalStand.Melee;
using Content.Shared.Mobs.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Melee;

// Pulsing mark over zombies the local player can execute with their next melee hit.
public sealed class FSExecutionMarkerOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    private const float Range = 9f;
    private static readonly Color MarkColor = Color.FromHex("#ff2a2a");

    private readonly FSMeleeJuiceSystem _juice;
    private readonly Vector2[] _arrow = new Vector2[3];
    private SharedTransformSystem? _xform;
    private MeleeWeaponSystem? _melee;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FSExecutionMarkerOverlay(FSMeleeJuiceSystem juice)
    {
        IoCManager.InjectDependencies(this);
        _juice = juice;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _xform ??= _entities.System<SharedTransformSystem>();
        _melee ??= _entities.System<MeleeWeaponSystem>();

        if (_player.LocalEntity is not { } local
            || !_melee.TryGetWeapon(local, out var weapon, out _)
            || !_entities.TryGetComponent<FSMeleeJuiceComponent>(weapon, out var juice))
            return;

        var handle = args.WorldHandle;
        var origin = _xform.GetWorldPosition(local);
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var time = (float) _timing.RealTime.TotalSeconds;
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 9f);

        // Every mob, since Hemorrhage opens bleeding ones up without a stagger.
        var query = _entities.EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != args.MapId || !_juice.CanExecute(uid, (weapon, juice)))
                continue;

            var pos = _xform.GetWorldPosition(xform);
            if ((pos - origin).LengthSquared() > Range * Range || !args.WorldBounds.Contains(pos))
                continue;

            handle.SetTransform(Matrix3Helpers.CreateTransform(pos, -rotation));
            var colour = MarkColor.WithAlpha(0.55f + 0.45f * pulse);
            handle.DrawCircle(Vector2.Zero, 0.42f + 0.05f * pulse, colour, false);
            handle.DrawCircle(Vector2.Zero, 0.46f + 0.05f * pulse, colour.WithAlpha(colour.A * 0.5f), false);

            var bob = 0.08f * pulse;
            _arrow[0] = new Vector2(-0.14f, 0.82f + bob);
            _arrow[1] = new Vector2(0.14f, 0.82f + bob);
            _arrow[2] = new Vector2(0f, 0.62f + bob);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _arrow, colour);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
