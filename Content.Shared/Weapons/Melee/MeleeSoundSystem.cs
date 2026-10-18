using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Melee;

/// <summary>
/// This handles <see cref="MeleeSoundComponent"/>
/// </summary>
public sealed partial class MeleeSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!; // FINALSTAND
    [Dependency] private IGameTiming _timing = default!; // FINALSTAND

    public const float DamagePitchVariation = 0.05f;

    // FINALSTAND: a crowd clawing one target sent hundreds of networked sounds a second. The server
    // plays at most one hit sound per target per interval; the attacker's own predicted sound is unaffected.
    private static readonly TimeSpan HitSoundInterval = TimeSpan.FromSeconds(0.15);
    private const int HitSoundPruneAt = 256;
    private readonly Dictionary<EntityUid, TimeSpan> _nextHitSound = new();

    // FINALSTAND: same for NPC swings, which mostly whiff in a crowd. Gated per 2x2 tile area instead of
    // per attacker, since each NPC only swings about once a second.
    private const float SwingCellSize = 2f;
    private readonly Dictionary<(EntityUid, Vector2i), TimeSpan> _nextSwingSound = new();

    /// <summary>
    /// Plays the SwingSound from a weapon component
    /// for immediate feedback, misses and such
    /// (Swinging a weapon goes "whoosh" whether it hits or not)
    /// </summary>
    public void PlaySwingSound(EntityUid userUid, EntityUid weaponUid, MeleeWeaponComponent weaponComponent)
    {
        // FINALSTAND
        if (_net.IsServer && !HasComp<ActorComponent>(userUid) && !TryReserveSwingSound(userUid))
            return;

        _audio.PlayPredicted(weaponComponent.SwingSound, weaponUid, userUid);
    }

    /// <summary>
    /// Takes a "damageType" string as an argument and uses it to
    /// search one of the various Dictionaries in the MeleeSoundComponent
    /// for a sound to play, and falls back if that fails
    /// </summary>
    /// <param name="damageType"> Serves as a lookup key for a hit sound </param>
    /// <param name="hitSoundOverride"> A sound can be supplied by the <see cref="MeleeHitEvent"/> itself to override everything else </param>
    public void PlayHitSound(EntityUid targetUid, EntityUid? userUid, string? damageType, SoundSpecifier? hitSoundOverride, MeleeWeaponComponent weaponComponent)
    {
        var hitSound      = weaponComponent.HitSound;
        var noDamageSound = weaponComponent.NoDamageSound;

        var playedSound = false;

        if (Deleted(targetUid))
            return;

        // FINALSTAND
        if (_net.IsServer && !TryReserveHitSound(targetUid))
            return;

        // hitting can obv destroy an entity so we play at coords and not following them
        var coords = Transform(targetUid).Coordinates;
        // Play sound based off of highest damage type.
        if (TryComp<MeleeSoundComponent>(targetUid, out var damageSoundComp))
        {
            if (damageType == null && damageSoundComp.NoDamageSound != null)
            {
                _audio.PlayPredicted(damageSoundComp.NoDamageSound, coords, userUid, damageSoundComp.NoDamageSound.Params.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (damageType != null && damageSoundComp.SoundTypes?.TryGetValue(damageType, out var damageSoundType) == true)
            {
                _audio.PlayPredicted(damageSoundType, coords, userUid, damageSoundType.Params.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (damageType != null && damageSoundComp.SoundGroups?.TryGetValue(damageType, out var damageSoundGroup) == true)
            {
                _audio.PlayPredicted(damageSoundGroup, coords, userUid, damageSoundGroup.Params.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        // Use weapon sounds if the thing being hit doesn't specify its own sounds.
        if (!playedSound)
        {
            if (hitSoundOverride != null)
            {
                _audio.PlayPredicted(hitSoundOverride, coords, userUid, hitSoundOverride.Params.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (hitSound != null)
            {
                _audio.PlayPredicted(hitSound, coords, userUid, hitSound.Params.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else
            {
                _audio.PlayPredicted(noDamageSound, coords, userUid, noDamageSound.Params.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        // Fallback to generic sounds.
        if (!playedSound)
        {
            switch (damageType)
            {
                // Unfortunately heat returns caustic group so can't just use the damagegroup in that instance.
                case "Burn":
                case "Heat":
                case "Radiation":
                case "Cold":
                    _audio.PlayPredicted(new SoundPathSpecifier("/Audio/Items/welder.ogg"), targetUid, userUid, AudioParams.Default.WithVariation(DamagePitchVariation));
                    break;
                // No damage, fallback to tappies
                case null:
                    _audio.PlayPredicted(new SoundCollectionSpecifier("WeakHit"), targetUid, userUid, AudioParams.Default.WithVariation(DamagePitchVariation));
                    break;
                case "Brute":
                    _audio.PlayPredicted(new SoundCollectionSpecifier("MetalThud"), targetUid, userUid, AudioParams.Default.WithVariation(DamagePitchVariation));
                    break;
            }
        }
    }

    // FINALSTAND
    private bool TryReserveHitSound(EntityUid target)
    {
        var now = _timing.CurTime;
        if (_nextHitSound.TryGetValue(target, out var next) && now < next)
            return false;

        if (_nextHitSound.Count >= HitSoundPruneAt)
        {
            foreach (var (uid, until) in _nextHitSound)
            {
                if (until <= now)
                    _nextHitSound.Remove(uid);
            }
        }

        _nextHitSound[target] = now + HitSoundInterval;
        return true;
    }

    // FINALSTAND
    private bool TryReserveSwingSound(EntityUid user)
    {
        var xform = Transform(user);
        if (xform.ParentUid is not { Valid: true } parent)
            return true;

        var key = (parent, (xform.LocalPosition / SwingCellSize).Floored());
        var now = _timing.CurTime;
        if (_nextSwingSound.TryGetValue(key, out var next) && now < next)
            return false;

        if (_nextSwingSound.Count >= HitSoundPruneAt)
        {
            foreach (var (cell, until) in _nextSwingSound)
            {
                if (until <= now)
                    _nextSwingSound.Remove(cell);
            }
        }

        _nextSwingSound[key] = now + HitSoundInterval;
        return true;
    }

}
