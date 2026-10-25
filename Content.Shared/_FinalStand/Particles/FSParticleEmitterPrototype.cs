using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Particles;

// Pure data for the client particle engine; lives in Shared so the server accepts the YAML.
[Prototype("fsParticleEmitter")]
public sealed partial class FSParticleEmitterPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public ResPath? Texture;

    [DataField]
    public SpriteSpecifier.Rsi? Animation;

    [DataField]
    public bool AlignToHeading;

    [DataField]
    public int Burst;

    // Particles per second while attached to an entity.
    [DataField]
    public float Rate;

    [DataField]
    public Vector2 Lifetime = new(0.6f, 1f);

    [DataField]
    public Vector2 Speed = Vector2.Zero;

    // Degrees; 90 is straight up the screen.
    [DataField]
    public float Direction = 90f;

    // Direction is measured from the attached entity's facing instead of the screen, so 90 is straight ahead.
    [DataField]
    public bool RotateWithEntity;

    // Tiles along the emission direction from the origin where particles appear.
    [DataField]
    public float ForwardOffset;

    // Each particle stops where a ray along its path first meets something on the Impassable layer.
    [DataField]
    public bool StopAtWalls;

    [DataField]
    public float Spread = 360f;

    [DataField]
    public Vector2 Acceleration = Vector2.Zero;

    [DataField]
    public float Drag;

    // Start and end size in tiles.
    [DataField]
    public Vector2 Size = new(0.25f, 0.25f);

    [DataField]
    public float SizeJitter;

    [DataField]
    public float SpawnRadius;

    [DataField]
    public Vector2 Offset = Vector2.Zero;

    [DataField]
    public Color Color = Color.White;

    // Colour by the end of its life, blended from Color; unset keeps Color throughout.
    [DataField]
    public Color? EndColor;

    // Multiply by the caller's colour, so one emitter serves every medic's beam colour.
    [DataField]
    public bool Tinted = true;

    // Fractions of lifetime spent fading in and out.
    [DataField]
    public float FadeIn = 0.1f;

    [DataField]
    public float FadeOut = 0.4f;

    // Degrees per second.
    [DataField]
    public Vector2 Spin = Vector2.Zero;

    // Streaks point along their velocity instead of spinning.
    [DataField]
    public bool AlignToVelocity;

    [DataField]
    public float Stretch = 1f;
}
