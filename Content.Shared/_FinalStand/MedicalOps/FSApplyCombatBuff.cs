using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSApplyCombatBuffSystem : EntityEffectSystem<FSFriendlyFireComponent, FSApplyCombatBuff>
{
    [Dependency] private FSMedicalBonusSystem _bonus = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier Landed = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
    private static readonly SoundSpecifier Refreshed = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");

    protected override void Effect(Entity<FSFriendlyFireComponent> entity, ref EntityEffectEvent<FSApplyCombatBuff> args)
    {
        var effect = args.Effect;

        // Standing in a cloud re-applies once a second, which reset the timer and replayed the
        // sound every tick. One dose runs its full duration instead.
        if (_bonus.HasBuff(entity, effect.Source))
            return;

        var refreshed = false;

        _bonus.ApplyBuff(entity,
            effect.Source,
            effect.Bonuses,
            TimeSpan.FromSeconds(effect.Duration * args.Scale),
            effect.Name is { } name ? Loc.GetString(name) : null);

        _audio.PlayPvs(refreshed ? Refreshed : Landed, entity, AudioParams.Default.WithVolume(refreshed ? -8f : -2f));
    }
}

public sealed partial class FSApplyCombatBuff : EntityEffectBase<FSApplyCombatBuff>
{
    public const string SourcePrefix = "chem-";

    [DataField(required: true)]
    public Dictionary<FSMedicalBonusCategory, float> Bonuses = new();

    [DataField]
    public float Duration = 90f;

    [DataField]
    public string Source = SourcePrefix + "generic";

    [DataField]
    public LocId? Name;
}
