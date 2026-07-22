using Content.Shared._FinalStand.Perks;

namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSPerkLevelsComponent : Component
{
    public Dictionary<string, int> Levels { get; set; } = new();
    public string[] Slots { get; set; } = new string[FSPerkDef.SlotCount];
    public string[][] Loadouts { get; set; } = new string[3][];

    public FSPerkLevelsComponent()
    {
        for (var i = 0; i < FSPerkDef.SlotCount; i++)
            Slots[i] = string.Empty;
        for (var i = 0; i < 3; i++)
        {
            Loadouts[i] = new string[FSPerkDef.SlotCount];
            for (var j = 0; j < FSPerkDef.SlotCount; j++)
                Loadouts[i][j] = string.Empty;
        }
    }

    public int GetLevel(string id) => Levels.TryGetValue(id, out var lvl) ? lvl : 0;

    public int GetSlottedLevel(string id)
    {
        if (!Slots.Contains(id)) return 0;
        return GetLevel(id);
    }
}
