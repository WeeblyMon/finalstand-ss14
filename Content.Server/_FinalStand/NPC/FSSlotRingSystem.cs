// Assigns each wave zombie a unique angular slot around the CCC.
using System.Numerics;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.NPC;

public sealed class FSSlotRingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly (int Count, float Radius)[] Rings =
    [
        (8,  1.0f),
        (16, 1.5f),
        (24, 1.9f),
    ];

    private readonly Dictionary<(EntityUid, int, int), EntityUid> _claimed = new();
    private readonly Dictionary<EntityUid, (EntityUid ccc, int ring, int slot)> _zombieSlot = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSlotClaimComponent, ComponentShutdown>(OnSlotShutdown);
        SubscribeLocalEvent<FSSlotClaimComponent, MobStateChangedEvent>(OnSlotMobStateChanged);
    }

    private void OnSlotShutdown(EntityUid uid, FSSlotClaimComponent _, ComponentShutdown args)
    {
        ReleaseSlot(uid);
    }

    // Corpses linger until the cleanup cap evicts them. Without this they hold their ring slot
    // the whole time, and the rings only hold 48 — the horde outgrows them and later zombies
    // all fall back to the same offset.
    private void OnSlotMobStateChanged(EntityUid uid, FSSlotClaimComponent _, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            ReleaseSlot(uid);
    }

    public Vector2 GetOrAssignSlot(EntityUid zombie, EntityUid ccc)
    {
        if (_zombieSlot.TryGetValue(zombie, out var existing) && existing.ccc == ccc)
            return SlotOffset(existing.ring, existing.slot);

        if (_zombieSlot.ContainsKey(zombie))
            ReleaseSlot(zombie);

        var cccPos = _transform.GetWorldPosition(ccc);
        var mobPos = _transform.GetWorldPosition(zombie);
        var delta  = mobPos - cccPos;
        var approachAngle = delta.LengthSquared() > 0.001f
            ? MathF.Atan2(delta.Y, delta.X)
            : 0f;

        for (var r = 0; r < Rings.Length; r++)
        {
            var count    = Rings[r].Count;
            var bestSlot = -1;
            var bestDist = float.MaxValue;

            for (var s = 0; s < count; s++)
            {
                if (_claimed.ContainsKey((ccc, r, s)))
                    continue;

                var slotAngle = (float)s / count * MathF.Tau;
                var d = AngularDistance(approachAngle, slotAngle);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestSlot = s;
                }
            }

            if (bestSlot < 0)
                continue;

            _claimed[(ccc, r, bestSlot)] = zombie;
            _zombieSlot[zombie] = (ccc, r, bestSlot);

            if (!TryComp<FSSlotClaimComponent>(zombie, out var claim))
                claim = AddComp<FSSlotClaimComponent>(zombie);

            claim.CCC       = ccc;
            claim.Ring      = r;
            claim.SlotIndex = bestSlot;

            return SlotOffset(r, bestSlot);
        }

        return Vector2.Zero;
    }

    private void ReleaseSlot(EntityUid zombie)
    {
        if (!_zombieSlot.TryGetValue(zombie, out var info))
            return;

        _zombieSlot.Remove(zombie);
        _claimed.Remove((info.ccc, info.ring, info.slot));
    }

    private static Vector2 SlotOffset(int ring, int slot)
    {
        var (count, radius) = Rings[ring];
        var angle = (float)slot / count * MathF.Tau;
        return new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
    }

    private static float AngularDistance(float a, float b)
    {
        var diff = MathF.Abs(a - b) % MathF.Tau;
        return diff > MathF.PI ? MathF.Tau - diff : diff;
    }
}
