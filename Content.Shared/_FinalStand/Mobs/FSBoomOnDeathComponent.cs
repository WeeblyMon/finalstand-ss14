using Robust.Shared.Audio;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent]
public sealed partial class FSBoomOnDeathComponent : Component
{
    [DataField] public float ExplosionRadius = 3f;
    [DataField] public float ExplosionDamage = 12f;
    [DataField] public float ToxinDamage = 39f;
    [DataField] public float SlowRadius = 3f;
    [DataField] public float SlowDuration = 5f;
    [DataField] public float SlowAmount = 0.775f;     // ~22.5% speed reduction (was 75%)
    [DataField] public int FlashCount = 2;
    [DataField] public float FlashInterval = 0.3f;
    [DataField] public float AggroVisionRadius = 20f;
    [DataField] public float ProximityRange = 3f;
    [DataField] public SoundSpecifier ExplosionSound =
        new SoundPathSpecifier("/Audio/_FinalStand/Mobs/Bloater/bloater_explode.ogg")
        {
            Params = AudioParams.Default.WithVolume(10f),
        };
    public bool PendingExplosion = false;
    public float ExplosionTimer = 0f;
    public bool Exploded = false;
}
