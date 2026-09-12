using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSApplyCombatBuffSystem : EntityEffectSystem<FSFriendlyFireComponent, FSApplyCombatBuff>
{
    [Dependency] private FSMedicalBonusSystem _bonus = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(2);

    private static readonly SoundSpecifier Landed = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    private static readonly SoundSpecifier Refreshed =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/buff_refresh.ogg");

    // Only a dose landing on a nearly-spent buff counts as a top-up.
    private const float RefreshFraction = 0.34f;

    protected override void Effect(Entity<FSFriendlyFireComponent> entity, ref EntityEffectEvent<FSApplyCombatBuff> args)
    {
        var effect = args.Effect;
        var duration = TimeSpan.FromSeconds(effect.Duration * args.Scale);

        // A cloud re-applies once a second and bloodstream metabolism ticks too, so re-applying
        // freely would let one dose run forever. Holding off until the buff is nearly spent keeps a
        // single dose to its stated duration while still letting a medic deliberately top someone up.
        var refreshing = _bonus.TryGetBuff(entity, effect.Source, out var existing);

        if (refreshing
            && (existing?.EndTime is not { } end || end - _timing.CurTime > duration * RefreshFraction))
        {
            return;
        }

        _bonus.ApplyBuff(entity,
            effect.Source,
            effect.Bonuses,
            duration,
            effect.Name is { } name ? Loc.GetString(name) : null);

        _audio.PlayPvs(refreshing ? Refreshed : Landed, entity,
            AudioParams.Default.WithVolume(refreshing ? -8f : -2f));

        var flash = EnsureComp<FSBuffFlashComponent>(entity);
        flash.Colour = effect.Colour;
        flash.EndTime = _timing.CurTime + FlashDuration;
        Dirty(entity, flash);
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

    [DataField]
    public Color Colour = Color.FromHex("#4FBF7A");
}
