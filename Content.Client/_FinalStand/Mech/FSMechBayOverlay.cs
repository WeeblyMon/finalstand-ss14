// A row of part icons under an unfinished Mech Bay: fitted parts solid, the ones that can go on now pulsing, the rest dim.
using System.Numerics;
using Content.Shared._FinalStand.Mech;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSMechBayOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float ShowRange = 8f;
    private const float IconSize = 0.42f;
    private const float Gap = 0.04f;
    private const float BelowCenter = 1.75f;
    private static readonly Color Missing = Color.FromHex("#6b7280");
    private static readonly Color Open = Color.FromHex("#f59e0b");

    private SharedTransformSystem? _xform;
    private SpriteSystem? _sprite;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FSMechBayOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _sprite ??= _entMan.System<SpriteSystem>();

        var me = _xform.GetMapCoordinates(local);
        var pulse = 0.55f + 0.35f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 4f);
        var handle = args.WorldHandle;
        var query = _entMan.EntityQueryEnumerator<FSMechBayComponent, TransformComponent>();
        while (query.MoveNext(out var bay, out var xform))
        {
            if (bay.Ready || xform.MapID != args.MapId || xform.MapID != me.MapId)
                continue;

            var center = _xform.GetWorldPosition(xform);
            if ((center - me.Position).Length() > ShowRange)
                continue;

            var width = bay.Slots.Count * (IconSize + Gap) - Gap;
            var x = center.X - width / 2f;
            var y = center.Y - BelowCenter;
            handle.DrawRect(new Box2(x - 0.06f, y - 0.06f, x + width + 0.06f, y + IconSize + 0.06f), Color.Black.WithAlpha(0.45f));

            foreach (var slot in bay.Slots)
            {
                var box = new Box2(x, y, x + IconSize, y + IconSize);
                var texture = _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(bay.Sprite), slot.Icon));
                var color = bay.Installed.Contains(slot.Id) ? Color.White
                    : bay.CanInstall(slot) ? Open.WithAlpha(pulse)
                    : Missing.WithAlpha(0.45f);
                handle.DrawTextureRect(texture, box, color);
                x += IconSize + Gap;
            }
        }
    }
}
