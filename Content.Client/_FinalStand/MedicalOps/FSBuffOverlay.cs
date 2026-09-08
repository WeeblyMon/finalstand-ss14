using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSBuffOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public const string ChemSourcePrefix = "chem-";

    private static readonly Color BuffColor = Color.FromHex("#4FBF7A");

    private const float Radius = 0.11f;
    private const float FadeWindow = 15f;

    public FSBuffOverlay(IEntityManager entManager, IGameTiming timing)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = _timing.CurTime;

        var query = _entManager.EntityQueryEnumerator<FSMedicalBonusComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bonus, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!TryGetChemBuff(bonus, now, out var remaining))
                continue;

            var worldPos = _transform.GetWorldPosition(xform);

            var height = 1f;
            if (_entManager.TryGetComponent(uid, out SpriteComponent? sprite))
                height = _sprite.GetLocalBounds((uid, sprite)).Height;

            var centre = worldPos + new Vector2(0f, height / 2f + 0.35f);

            if (!Box2.CenteredAround(centre, new Vector2(0.5f, 0.5f)).Intersects(args.WorldAABB))
                continue;

            var alpha = remaining is { } left
                ? Math.Clamp(left / FadeWindow, 0.25f, 1f)
                : 1f;

            handle.DrawCircle(centre, Radius, BuffColor.WithAlpha(alpha));
        }
    }

    private static bool TryGetChemBuff(FSMedicalBonusComponent bonus, TimeSpan now, out float? remaining)
    {
        remaining = null;

        foreach (var (source, buff) in bonus.Active)
        {
            if (!source.StartsWith(ChemSourcePrefix) || buff.IsExpired(now))
                continue;

            if (buff.EndTime is { } end)
                remaining = (float) (end - now).TotalSeconds;

            return true;
        }

        return false;
    }
}
