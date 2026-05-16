using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Systems;
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
        SubscribeLocalEvent<ActorComponent, AttackAttemptEvent>(OnAttackAttempt);
    }
    public void AssignFactionToAllPlayers()
    {
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var uid, out _))
            TryAssignPlayerFaction(uid);
    }

    private void OnActorStartup(EntityUid uid, ActorComponent _, ComponentStartup args)
    {
        if (!IsWaveRuleActive())
            return;
        TryAssignPlayerFaction(uid);
    }

    private void OnAttackAttempt(EntityUid uid, ActorComponent _, AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        if (_npcFaction.IsEntityFriendly(uid, args.Target.Value))
            args.Cancel();
    }

    private void TryAssignPlayerFaction(EntityUid uid)
    {
        if (HasComp<GhostComponent>(uid) || HasComp<WaveSpawnedTagComponent>(uid))
            return;

        _npcFaction.AddFaction(uid, FsPlayerFaction);
    }

    private bool IsWaveRuleActive()
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var gameRule))
        {
            if (_gameTicker.IsGameRuleActive(uid, gameRule))
                return true;
        }
        return false;
    }
}
