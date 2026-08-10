using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Interaction;
using Content.Shared.NPC.Systems;
using Content.Shared.Temperature;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.FriendlyFire;

public sealed class FSFriendlyFireSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private const string FsPlayerFaction = "FSPlayer";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, ComponentStartup>(OnActorStartup);
        SubscribeLocalEvent<FSFriendlyFireComponent, InteractUsingEvent>(OnInteractUsingFriendlyFire,
            before: new[] { typeof(FlammableSystem) });
    }

    private void OnInteractUsingFriendlyFire(EntityUid uid, FSFriendlyFireComponent _, InteractUsingEvent args)
    {
        if (args.Handled) return;
        if (!HasComp<FSFriendlyFireComponent>(args.User)) return;
        var isHot = new IsHotEvent();
        RaiseLocalEvent(args.Used, isHot);
        if (!isHot.IsHot) return;
        args.Handled = true;
    }

    public void AssignFactionToAllPlayers()
    {
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var uid, out _))
            TryAssignPlayerFaction(uid);
    }

    private void OnActorStartup(EntityUid uid, ActorComponent _, ComponentStartup args)
    {
        if (IsWaveRuleActive())
            TryAssignPlayerFaction(uid);
    }

    private void TryAssignPlayerFaction(EntityUid uid)
    {
        if (!HasComp<GhostComponent>(uid) && !HasComp<WaveSpawnedTagComponent>(uid))
        {
            _npcFaction.AddFaction(uid, FsPlayerFaction);
            EnsureComp<FSFriendlyFireComponent>(uid);
        }
    }

    private bool IsWaveRuleActive()
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var gameRule))
            if (_gameTicker.IsGameRuleActive(uid, gameRule)) return true;
        return false;
    }
}
