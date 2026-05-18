using Content.Shared._FinalStand.ReadyCheck;

namespace Content.Server._FinalStand.ReadyCheck;

[RegisterComponent]
public sealed partial class ReadyCheckComponent : Component
{
    public Dictionary<string, ReadyStatus> DepartmentStatus = new();
    public bool IsCombatPhase;

    public int ReadyCount
    {
        get
        {
            var n = 0;
            foreach (var s in DepartmentStatus.Values)
                if (s == ReadyStatus.Ready) n++;
            return n;
        }
    }
}
