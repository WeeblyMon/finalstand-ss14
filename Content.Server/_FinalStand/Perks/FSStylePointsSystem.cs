using Content.Shared._FinalStand.Perks;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSStylePointsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public float GetMultiplier(EntityUid mindId, FSPerkLevelsComponent perks, EntityUid gun)
    {
        var level = perks.GetSlottedLevel("StylePoints");
        if (level <= 0 || ProtoOf(gun) is not { } proto)
            return 1f;

        if (!TryComp<FSStylePointsComponent>(mindId, out var style) || !style.Guns.TryGetValue(proto, out var state))
            return 1f;

        return 1f + state.Charge * level * FSPerkBonusConstants.StylePointsPerLevel;
    }

    public float GetReadyMultiplier(EntityUid mindId, FSPerkLevelsComponent perks, EntityUid gun)
    {
        var level = perks.GetSlottedLevel("StylePoints");
        if (level <= 0 || ProtoOf(gun) is not { } proto)
            return 1f;

        var charge = TryComp<FSStylePointsComponent>(mindId, out var style) && style.Guns.TryGetValue(proto, out var state)
            ? Rested(state.Charge, _timing.CurTime - state.LastShot)
            : 1f;
        return 1f + charge * level * FSPerkBonusConstants.StylePointsPerLevel;
    }

    public void OnShot(EntityUid mindId, FSPerkLevelsComponent perks, EntityUid gun)
    {
        if (perks.GetSlottedLevel("StylePoints") > 0 && ProtoOf(gun) is { } proto)
            RecordShot(mindId, proto, _timing.CurTime);
    }

    public void RecordShot(EntityUid mindId, string proto, TimeSpan now)
    {
        var style = EnsureComp<FSStylePointsComponent>(mindId);
        if (!style.Guns.TryGetValue(proto, out var state))
        {
            style.Guns[proto] = (1f, now);
            return;
        }

        style.Guns[proto] = (Next(state.Charge, now - state.LastShot), now);
    }

    public static float Next(float charge, TimeSpan gap)
    {
        var seconds = (float) gap.TotalSeconds;
        return seconds >= FSPerkBonusConstants.StylePointsRestSeconds
            ? Rested(charge, gap)
            : MathF.Max(0f, charge - seconds / FSPerkBonusConstants.StylePointsDrainSeconds);
    }

    private static float Rested(float charge, TimeSpan gap)
    {
        var resting = (float) gap.TotalSeconds - FSPerkBonusConstants.StylePointsRestSeconds;
        return resting <= 0f ? charge : MathF.Min(1f, charge + resting / FSPerkBonusConstants.StylePointsRampSeconds);
    }

    private string? ProtoOf(EntityUid gun) => MetaData(gun).EntityPrototype?.ID;
}

[RegisterComponent]
public sealed partial class FSStylePointsComponent : Component
{
    public Dictionary<string, (float Charge, TimeSpan LastShot)> Guns = new();
}
