using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.RCD;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSEngineerOnlyRCDComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Caps = new();

    [DataField, AutoNetworkedField]
    public int StationCap = 120;

    // Filled by the server for whoever holds the RCD, so the menu and status line can show cap use.
    [AutoNetworkedField]
    public Dictionary<string, int> CapsUsed = new();

    [AutoNetworkedField]
    public int StationUsed;
}
