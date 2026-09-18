using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Mobs.Systems;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSMedicPingOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;
    private readonly MobStateSystem _mobState;
    private readonly SpriteSystem _sprite;

    private readonly Texture? _normal;
    private readonly Texture? _hurt;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private const float Lifetime = 2.5f;
    private const float BubbleMeters = 1.0f;

    private static readonly Color ChemTint = Color.FromHex("#4FBF7A");

    private readonly record struct Ping(EntityUid Target, bool IsHurt, float Life, FSPingKind Kind);
    private readonly List<Ping> _pings = new();

    public FSMedicPingOverlay(IEntityManager entManager, IResourceCache cache)
    {
        _entManager = entManager;
        _transform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _mobState = _entManager.System<MobStateSystem>();

        _normal = TryLoad(cache, "normal");
        _hurt = TryLoad(cache, "hurt");
    }

    private static Texture? TryLoad(IResourceCache cache, string name)
    {
        return FSOverlayTextures.TryLoad(cache, $"/Textures/_FinalStand/Interface/MedicPing/{name}.png");
    }

    public void Add(EntityUid target, bool isHurt, FSPingKind kind)
    {
        _pings.RemoveAll(p => p.Target == target);
        _pings.Add(new Ping(target, isHurt, Lifetime, kind));
    }

    public void Clear() => _pings.Clear();

    protected override void FrameUpdate(FrameEventArgs args)
    {
        for (var i = _pings.Count - 1; i >= 0; i--)
        {
            var updated = _pings[i] with { Life = _pings[i].Life - args.DeltaSeconds };
            if (updated.Life <= 0f || !_entManager.EntityExists(updated.Target))
                _pings.RemoveAt(i);
            else
                _pings[i] = updated;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_pings.Count == 0)
            return;

        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        foreach (var ping in _pings)
        {
            var texture = ping.IsHurt ? _hurt : _normal;
            if (texture == null)
                continue;

            if (!_entManager.TryGetComponent(ping.Target, out TransformComponent? xform)
                || xform.MapID != args.MapId)
                continue;

            if (_mobState.IsIncapacitated(ping.Target))
                continue;

            var worldPos = _transform.GetWorldPosition(xform);

            var height = 1f;
            if (_entManager.TryGetComponent(ping.Target, out SpriteComponent? sprite))
                height = _sprite.GetLocalBounds((ping.Target, sprite)).Height;

            var bounds = Box2.CenteredAround(worldPos, new Vector2(BubbleMeters, BubbleMeters));
            if (!bounds.Intersects(args.WorldAABB))
                continue;

            var alpha = Math.Clamp(ping.Life / (Lifetime / 3f), 0f, 1f);

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));

            var half = BubbleMeters / 2f;
            var yOffset = height / 2f + half * 0.6f;
            var box = new Box2(-half, yOffset - half, half, yOffset + half);

            var tint = ping.Kind == FSPingKind.Chem ? ChemTint : Color.White;
            handle.DrawTextureRect(texture, box, tint.WithAlpha(alpha));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
