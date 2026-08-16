using Content.Server._FinalStand.CCC;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;

namespace Content.Server._FinalStand.ReadyCheck;

public sealed class ReadyCheckSystem : EntitySystem
{
    private Entity<ReadyCheckComponent>? _cached;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<ReadyCheckComponent, ComponentShutdown>(OnReadyCheckShutdown);
    }

    private void OnReadyCheckShutdown(Entity<ReadyCheckComponent> ent, ref ComponentShutdown args)
    {
        if (_cached?.Owner == ent.Owner)
            _cached = null;
    }

    private ReadyCheckComponent? FindCCCReadyCheck()
    {
        if (_cached is { } cached && !TerminatingOrDeleted(cached.Owner))
            return cached.Comp;

        var q = EntityQueryEnumerator<FinalStandCCCComponent, ReadyCheckComponent>();
        while (q.MoveNext(out var uid, out _, out var rc))
        {
            _cached = (uid, rc);
            return rc;
        }

        _cached = null;
        return null;
    }

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        var rc = FindCCCReadyCheck();
        if (rc == null) return;
        rc.IsCombatPhase = false;
        rc.ReadiedPlayers.Clear();
        RaiseLocalEvent(new ReadyCheckUpdatedEvent());
    }

    private void OnCombatStarted(WaveCombatStartedEvent ev)
    {
        var rc = FindCCCReadyCheck();
        if (rc == null) return;
        rc.IsCombatPhase = true;
        RaiseLocalEvent(new ReadyCheckUpdatedEvent());
    }

    public void SetPlayerReady(EntityUid player, bool ready)
    {
        var rc = FindCCCReadyCheck();
        if (rc == null) return;
        if (ready)
            rc.ReadiedPlayers.Add(player);
        else
            rc.ReadiedPlayers.Remove(player);
        RaiseLocalEvent(new ReadyCheckUpdatedEvent());
    }

    public void SetTotalPlayers(int count)
    {
        var rc = FindCCCReadyCheck();
        if (rc != null) rc.TotalPlayers = count;
    }

    public void ResetReadyStates()
    {
        var rc = FindCCCReadyCheck();
        if (rc == null) return;
        rc.ReadiedPlayers.Clear();
        rc.IsCombatPhase = false;
    }

    public bool IsCombatPhase() => FindCCCReadyCheck()?.IsCombatPhase ?? false;
    public int GetReadyCount() => FindCCCReadyCheck()?.ReadyCount ?? 0;
    public int GetTotalCount() => FindCCCReadyCheck()?.TotalPlayers ?? 0;
    public bool HasMajority() => FindCCCReadyCheck()?.HasMajority ?? false;
    public bool IsPlayerReady(EntityUid player) => FindCCCReadyCheck()?.ReadiedPlayers.Contains(player) ?? false;
}
