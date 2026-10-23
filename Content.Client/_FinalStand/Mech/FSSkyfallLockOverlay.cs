using System.Numerics;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSSkyfallLockOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;

    private static readonly Color LockColor = Color.FromHex("#ff3b30");
    private static readonly ResPath FontPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private const float Size = 34f;
    private const float SnapTime = 0.22f;
    private const float Corner = 0.32f;
    private const float Thickness = 2f;
    private const float SearchRange = 9f;
    private const int Swarm = 12;
    private static readonly ProtoId<NpcFactionPrototype> ZombieFaction = "FSZombie";
    private static readonly Color ReloadColor = Color.FromHex("#ffb35c");
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromSeconds(0.1);
    private static readonly (float X, float Y)[] Corners = { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) };

    private readonly Dictionary<EntityUid, TimeSpan> _since = new();
    private readonly Dictionary<EntityUid, int> _counts = new();
    private readonly List<EntityUid> _gone = new();
    private SharedTransformSystem? _xform;
    private EntityLookupSystem? _lookup;
    private SharedInteractionSystem? _interaction;
    private MobStateSystem? _mobState;
    private NpcFactionSystem? _faction;
    private FSMechWeaponSelectSystem? _weapons;
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();
    private readonly List<(EntityUid Target, MapCoordinates At, float Distance)> _candidates = new();
    private readonly List<EntityUid> _preview = new();
    private readonly SharedInteractionSystem.Ignored _ignore;
    private EntityUid _ignoreMech;
    private TimeSpan _nextPreview;
    private Font? _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSSkyfallLockOverlay()
    {
        IoCManager.InjectDependencies(this);
        _ignore = e => e == _ignoreMech || _entMan.HasComponent<MobStateComponent>(e);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _font ??= new VectorFont(_cache.GetResource<FontResource>(FontPath), 8);
        var now = _timing.RealTime;

        _counts.Clear();
        var query = _entMan.EntityQueryEnumerator<FSSkyfallMissileComponent>();
        while (query.MoveNext(out var missile))
        {
            if (missile.LockedOn is { } target && _entMan.EntityExists(target))
                _counts[target] = _counts.GetValueOrDefault(target) + 1;
        }

        foreach (var target in _since.Keys)
        {
            if (!_counts.ContainsKey(target))
                _gone.Add(target);
        }

        foreach (var target in _gone)
            _since.Remove(target);
        _gone.Clear();

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        DrawPilot(handle, matrix, args.MapId, now);
        foreach (var (target, count) in _counts)
        {
            var world = _xform.GetMapCoordinates(target);
            if (world.MapId != args.MapId)
                continue;

            if (!_since.TryGetValue(target, out var since))
                _since[target] = since = now;

            var t = Math.Clamp((float) (now - since).TotalSeconds / SnapTime, 0f, 1f);
            var ease = 1f - (1f - t) * (1f - t);
            var half = Size * (2f - ease) / 2f;
            var pulse = t < 1f ? 1f : 0.7f + 0.3f * MathF.Sin((float) now.TotalSeconds * 12f);
            var color = LockColor.WithAlpha(pulse);
            var center = Vector2.Transform(world.Position, matrix);
            Brackets(handle, center, half, color);

            if (t < 1f)
                continue;

            var label = count > 1 ? Loc.GetString("fs-skyfall-lock-many", ("count", count)) : Loc.GetString("fs-skyfall-lock");
            handle.DrawString(_font, center + new Vector2(half + 3f, -half - 2f), label, color);
        }
    }

    private EntityUid? ArmedPod(EntityUid mech, MechComponent mechComp)
    {
        _weapons ??= _entMan.System<FSMechWeaponSelectSystem>();
        foreach (var equipment in mechComp.EquipmentContainer.ContainedEntities)
        {
            if (_entMan.HasComponent<FSSkyfallLauncherComponent>(equipment) && _weapons.IsArmed(mech, equipment))
                return equipment;
        }

        return null;
    }

    private void DrawPilot(DrawingHandleScreen handle, Matrix3x2 matrix, MapId map, TimeSpan now)
    {
        if (_player.LocalEntity is not { } local
            || !_entMan.TryGetComponent<VehicleOperatorComponent>(local, out var op)
            || op.Vehicle is not { } mech
            || !_entMan.TryGetComponent<MechComponent>(mech, out var mechComp)
            || ArmedPod(mech, mechComp) is not { } pod
            || !_entMan.TryGetComponent<GunComponent>(pod, out var gun))
        {
            ResetPreview();
            return;
        }

        var left = (float) (gun.NextFire - _timing.CurTime).TotalSeconds;
        if (left > 0.3f)
        {
            var at = _input.MouseScreenPosition.Position + new Vector2(16f, 14f);
            var progress = Math.Clamp(1f - left / MathF.Max(gun.BurstCooldown, 0.01f), 0f, 1f);
            handle.DrawString(_font!, at, Loc.GetString("fs-skyfall-reload", ("seconds", MathF.Ceiling(left))), ReloadColor);
            var bar = UIBox2.FromDimensions(at + new Vector2(0f, 14f), new Vector2(54f, 4f));
            handle.DrawRect(bar, Color.Black.WithAlpha(0.6f));
            handle.DrawRect(UIBox2.FromDimensions(bar.TopLeft, new Vector2(54f * progress, 4f)), ReloadColor);
            ResetPreview();
            return;
        }

        if (_counts.Count > 0)
        {
            ResetPreview();
            return;
        }

        _mobState ??= _entMan.System<MobStateSystem>();

        // The preview is a hint, so the target pick runs at 10 Hz; brackets still follow the targets every frame.
        if (now >= _nextPreview)
        {
            _nextPreview = now + PreviewInterval;
            RefreshPreview(mech);
        }

        var color = LockColor.WithAlpha(0.3f + 0.15f * MathF.Sin((float) now.TotalSeconds * 4f));
        foreach (var target in _preview)
        {
            if (!_mobState.IsAlive(target))
                continue;

            var world = _xform!.GetMapCoordinates(target);
            if (world.MapId == map)
                Brackets(handle, Vector2.Transform(world.Position, matrix), Size * 0.6f, color);
        }
    }

    private void RefreshPreview(EntityUid mech)
    {
        _lookup ??= _entMan.System<EntityLookupSystem>();
        _interaction ??= _entMan.System<SharedInteractionSystem>();
        _faction ??= _entMan.System<NpcFactionSystem>();

        var eye = _xform!.GetMapCoordinates(mech);
        _ignoreMech = mech;
        _nearby.Clear();
        _candidates.Clear();
        _preview.Clear();
        _lookup.GetEntitiesInRange(eye, SearchRange, _nearby);
        foreach (var zombie in _nearby)
        {
            if (!_mobState!.IsAlive(zombie, zombie.Comp) || !_faction.IsMember(zombie.Owner, ZombieFaction))
                continue;

            var at = _xform.GetMapCoordinates(zombie);
            _candidates.Add((zombie.Owner, at, (at.Position - eye.Position).LengthSquared()));
        }

        // Nearest first, so only enough rays are cast to fill the swarm.
        _candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        foreach (var (target, at, _) in _candidates)
        {
            if (_preview.Count >= Swarm)
                break;

            if (_interaction.InRangeUnobstructed(eye, at, 0f, CollisionGroup.Opaque, _ignore))
                _preview.Add(target);
        }
    }

    private void ResetPreview()
    {
        _preview.Clear();
        _nextPreview = TimeSpan.Zero;
    }

    private static void Brackets(DrawingHandleScreen handle, Vector2 center, float half, Color color)
    {
        var arm = half * 2f * Corner;
        foreach (var (sx, sy) in Corners)
        {
            var corner = center + new Vector2(sx * half, sy * half);
            var x0 = sx < 0 ? corner.X : corner.X - arm;
            var y0 = sy < 0 ? corner.Y : corner.Y - Thickness;
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x0, y0), new Vector2(arm, Thickness)), color);
            var vx = sx < 0 ? corner.X : corner.X - Thickness;
            var vy = sy < 0 ? corner.Y : corner.Y - arm;
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(vx, vy), new Vector2(Thickness, arm)), color);
        }
    }
}
