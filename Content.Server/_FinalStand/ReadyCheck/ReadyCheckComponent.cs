namespace Content.Server._FinalStand.ReadyCheck;

[RegisterComponent]
public sealed partial class ReadyCheckComponent : Component
{
    public HashSet<EntityUid> ReadiedPlayers = new();
    public int TotalPlayers;
    public bool IsCombatPhase;

    public int ReadyCount => ReadiedPlayers.Count;
    public bool HasMajority => TotalPlayers > 0 && ReadiedPlayers.Count > TotalPlayers / 2;
}
