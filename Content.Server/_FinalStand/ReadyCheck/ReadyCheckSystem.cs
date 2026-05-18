using Content.Server._FinalStand.Station;
using Content.Shared._FinalStand.ReadyCheck;

namespace Content.Server._FinalStand.ReadyCheck;

public sealed class ReadyCheckSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
    }

    private ReadyCheckComponent? FindCCCReadyCheck()
    {
        var q = EntityQueryEnumerator<FinalStandCCCComponent, ReadyCheckComponent>();
        while (q.MoveNext(out _, out _, out var rc))
            return rc;
        return null;
    }

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        var rc = FindCCCReadyCheck();
        if (rc == null) return;

        rc.IsCombatPhase = false;
        rc.DepartmentStatus.Clear();
        foreach (var code in ReadyCheckDepts.AllDisplayCodes)
            rc.DepartmentStatus[code] = ReadyStatus.NoResponse;

        RaiseLocalEvent(new ReadyCheckUpdatedEvent());
    }

    private void OnCombatStarted(WaveCombatStartedEvent ev)
    {
        var rc = FindCCCReadyCheck();
        if (rc == null) return;

        rc.IsCombatPhase = true;
        RaiseLocalEvent(new ReadyCheckUpdatedEvent());
    }

    public bool SetDepartmentStatus(string deptCode, ReadyStatus status)
    {
        var rc = FindCCCReadyCheck();
        if (rc == null) return false;

        rc.DepartmentStatus[deptCode] = status;
        RaiseLocalEvent(new ReadyCheckUpdatedEvent());
        return true;
    }

    public Dictionary<string, ReadyStatus> GetStatuses()
    {
        var rc = FindCCCReadyCheck();
        return rc == null
            ? new Dictionary<string, ReadyStatus>()
            : new Dictionary<string, ReadyStatus>(rc.DepartmentStatus);
    }

    public bool IsCombatPhase() => FindCCCReadyCheck()?.IsCombatPhase ?? false;
    public int ReadyCount() => FindCCCReadyCheck()?.ReadyCount ?? 0;
}
