// Speedload perk: a flat reload speed bonus for every reload path.
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;

namespace Content.Server._FinalStand.Perks;

public sealed class FSSpeedloadSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public float GetReloadTimeMultiplier(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return 1f;

        var level = perks.GetSlottedLevel("Speedload");
        return level > 0 ? 1f / (1f + level * FSPerkBonusConstants.SpeedloadPerLevel) : 1f;
    }
}
