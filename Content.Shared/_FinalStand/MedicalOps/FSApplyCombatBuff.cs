using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.EntityEffects;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSApplyCombatBuffSystem : EntityEffectSystem<FSFriendlyFireComponent, FSApplyCombatBuff>
{
    [Dependency] private FSMedicalBonusSystem _bonus = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(2);

    private static readonly SoundSpecifier Landed = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    private static readonly SoundSpecifier Refreshed =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/buff_refresh.ogg");

    // A cloud touches every half second, so a buff this fresh is left alone.
    private const float FreshFraction = 0.9f;
    private const float AudibleFraction = 0.5f;

    protected override void Effect(Entity<FSFriendlyFireComponent> entity, ref EntityEffectEvent<FSApplyCombatBuff> args)
    {
        var effect = args.Effect;
        var duration = TimeSpan.FromSeconds(effect.Duration);

        var refreshing = _bonus.TryGetBuff(entity, effect.Source, out var existing);
        var left = TimeSpan.Zero;

        if (refreshing)
        {
            if (existing?.EndTime is not { } end)
                return;

            left = end - _timing.CurTime;
            if (left > duration * FreshFraction)
                return;
        }

        _bonus.ApplyBuff(entity,
            effect.Source,
            effect.Bonuses,
            duration,
            effect.Name is { } name ? Loc.GetString(name) : null);

        if (!refreshing)
        {
            _popup.PopupEntity(Loc.GetString("fs-buff-landed",
                    ("name", effect.Name is { } label ? Loc.GetString(label) : Loc.GetString("fs-buff-status")),
                    ("effects", string.Join(", ", FSMedicalBonusText.Describe(effect.Bonuses))),
                    ("seconds", (int) duration.TotalSeconds)),
                entity, entity, PopupType.Medium);
        }
        else if (left > duration * AudibleFraction)
        {
            return;
        }

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
