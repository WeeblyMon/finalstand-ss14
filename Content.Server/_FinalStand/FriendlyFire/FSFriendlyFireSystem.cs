using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.GameTicking;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.FriendlyFire;

public sealed class FSFriendlyFireSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string FsPlayerFaction = "FSPlayer";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, ComponentStartup>(OnActorStartup);
        SubscribeLocalEvent<ActorComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ActorComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
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

    private void OnAttackAttempt(EntityUid uid, ActorComponent _, AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        if (!HasComp<ActorComponent>(args.Target.Value))
            return;

        args.Cancel();

        // Apply weapon cooldown so the cancelled attack doesn't rapid-fire.
        if (args.Weapon is { } weapon)
        {
            var rate = weapon.Comp.AttackRate > 0f ? weapon.Comp.AttackRate : 1f;
            weapon.Comp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(1f / rate);
            DirtyField(weapon.Owner, weapon.Comp, nameof(MeleeWeaponComponent.NextAttack));
        }
    }

    private void OnBeforeDamage(EntityUid uid, ActorComponent _, ref BeforeDamageChangedEvent args)
    {
        // Block all damage whose origin is another player.
        if (args.Origin != null && args.Origin.Value != uid && HasComp<ActorComponent>(args.Origin.Value))
            args.Cancelled = true;
    }

    private void TryAssignPlayerFaction(EntityUid uid)
    {
        if (!HasComp<GhostComponent>(uid) && !HasComp<WaveSpawnedTagComponent>(uid))
            _npcFaction.AddFaction(uid, FsPlayerFaction);
    }

    private bool IsWaveRuleActive()
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var gameRule))
            if (_gameTicker.IsGameRuleActive(uid, gameRule)) return true;
        return false;
    }
}
