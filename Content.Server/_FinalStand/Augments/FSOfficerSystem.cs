using Content.Shared._FinalStand.Augments;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Augments;

// Owns (FSOfficerWhistleComponent, UseInHandEvent) — whistle buffs nearby allies.
public sealed class FSOfficerSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float WhistleRange = 10f;
    private static readonly TimeSpan BuffDuration = TimeSpan.FromSeconds(8);
    private static readonly EntProtoId WhistleProto = "FSOfficerWhistle";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSOfficerWhistleComponent, UseInHandEvent>(OnWhistleUse);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSOfficerBuffComponent>();
        while (query.MoveNext(out var uid, out var buff))
        {
            if (now >= buff.EndTime)
                RemComp<FSOfficerBuffComponent>(uid);
        }
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;
        if (augs.GetSlottedLevel("Officer") <= 0) return;

        var whistle = Spawn(WhistleProto, Transform(ev.Mob).Coordinates);
        if (_hands.TryPickupAnyHand(ev.Mob, whistle)) return;
        foreach (var slot in new[] { "pocket1", "pocket2", "belt" })
        {
            if (_inventory.TryEquip(ev.Mob, whistle, slot, silent: true)) return;
        }
        // fallback — drop at feet
    }

    private void OnWhistleUse(EntityUid uid, FSOfficerWhistleComponent comp, UseInHandEvent args)
    {
        if (!_mind.TryGetMind(args.User, out var mindId, out MindComponent? _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;
        var level = augs.GetSlottedLevel("Officer");
        if (level <= 0) return;

        var pos = _transform.GetMapCoordinates(args.User);
        var targets = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesInRange(pos, WhistleRange, targets);

        foreach (var (targetUid, _) in targets)
        {
            if (targetUid == args.User) continue;
            if (!_mind.TryGetMind(targetUid, out var targetMind, out MindComponent? _)) continue;
            var buff = EnsureComp<FSOfficerBuffComponent>(targetMind);
            buff.EndTime = _timing.CurTime + BuffDuration;
            buff.Level = level;
        }
    }
}
