using Content.Shared._FinalStand.Perks;

namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSPerkLevelsComponent : Component
{
    public Dictionary<string, int> Levels { get; set; } = new();
    public string[] Slots { get; set; } = new string[FSPerkDef.SlotCount];
    public FSPerkLoadout[] Loadouts { get; set; } = new FSPerkLoadout[3];

    public FSPerkLevelsComponent()
    {
        for (var i = 0; i < FSPerkDef.SlotCount; i++)
            Slots[i] = string.Empty;
        for (var i = 0; i < 3; i++)
            Loadouts[i] = new FSPerkLoadout();
    }

    private Dictionary<string, int>? _slotted;

    public void Invalidate() => _slotted = null;

    private Dictionary<string, int> Slotted()
    {
        if (_slotted != null)
            return _slotted;

        _slotted = new Dictionary<string, int>(FSPerkDef.SlotCount);
        foreach (var id in Slots)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            var level = GetLevel(id);
            if (level > 0)
                _slotted[id] = level;
        }
        return _slotted;
    }

    public int GetLevel(string id) => Levels.TryGetValue(id, out var lvl) ? lvl : 0;

    public int GetSlottedLevel(string id) => Slotted().TryGetValue(id, out var lvl) ? lvl : 0;
}
