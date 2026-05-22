using Content.Shared._FinalStand.Augments;

namespace Content.Server._FinalStand.Augments;

[RegisterComponent]
public sealed partial class FSAugmentLevelsComponent : Component
{
    public Dictionary<string, int> Levels { get; set; } = new();
    public string[] Slots { get; set; } = new string[FSAugmentDef.SlotCount];
    public string[][] Loadouts { get; set; } = new string[3][];

    public FSAugmentLevelsComponent()
    {
        for (var i = 0; i < FSAugmentDef.SlotCount; i++)
            Slots[i] = string.Empty;
        for (var i = 0; i < 3; i++)
        {
            Loadouts[i] = new string[FSAugmentDef.SlotCount];
            for (var j = 0; j < FSAugmentDef.SlotCount; j++)
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
