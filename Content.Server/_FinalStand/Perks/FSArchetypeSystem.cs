using Content.Shared._FinalStand.Perks;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSArchetypeSystem : EntitySystem
{
    public FSArchetype Get(EntityUid mindId)
        => TryComp<FSPerkLevelsComponent>(mindId, out var perks) ? Get(perks) : default;

    public static FSArchetype Get(FSPerkLevelsComponent perks)
        => FSArchetypeRules.Resolve(perks.Slots, perks.GetLevel);

    public FSArchetypeBonus Bonus(EntityUid mindId) => FSArchetypeRules.Bonus(Get(mindId));

    public static FSArchetypeBonus Bonus(FSPerkLevelsComponent perks) => FSArchetypeRules.Bonus(Get(perks));
}
