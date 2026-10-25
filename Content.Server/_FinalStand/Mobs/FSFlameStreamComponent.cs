using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Mobs;

// The flames a flamethrower zombie has in the air. Plain data on the server; clients draw their own particles.
[RegisterComponent, Access(typeof(FSFlamethrowerSystem))]
public sealed partial class FSFlameStreamComponent : Component
{
    public readonly List<FSFlame> Flames = new();

    // A flame sets each target alight once, like the projectile it replaces.
    public readonly HashSet<(int Flame, EntityUid Target)> Ignited = new();

    public int NextId;
    public float CheckAccumulator;
}

public struct FSFlame
{
    public int Id;
    public MapId Map;
    public Vector2 Origin;
    public Vector2 Direction;
    public float Speed;
    // How far it travels before a wall stops it.
    public float Reach;
    public float Age;
    public float Life;
    // Distance travelled at the last hit check.
    public float Checked;

    public readonly float Travelled => MathF.Min(Speed * Age, Reach);
}
