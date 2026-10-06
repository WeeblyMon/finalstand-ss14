using Content.Shared.RCD;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.RCD;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSEngineerOnlyRCDComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Caps = new();

    [DataField, AutoNetworkedField]
    public int StationCap = 120;

    [DataField, AutoNetworkedField]
    public float Reach = 2.5f;

    // Fortification pieces in menu order: ring slices clockwise from the top, belt slots left to right.
    [DataField]
    public List<ProtoId<RCDPrototype>> Palette = new();

    // Filled by the server for whoever holds the RCD, so the menu and status line can show cap use.
    [AutoNetworkedField]
    public Dictionary<string, int> CapsUsed = new();

    [AutoNetworkedField]
    public Dictionary<string, int> CapsMax = new();

    [AutoNetworkedField]
    public List<string> Locked = new();

    [AutoNetworkedField]
    public int StationUsed;

    // Sheets of each palette material the holder could draw from silos in reach.
    [AutoNetworkedField]
    public Dictionary<string, int> SiloStock = new();
}
