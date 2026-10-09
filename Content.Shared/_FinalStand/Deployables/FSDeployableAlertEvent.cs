using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Deployables;

[Serializable, NetSerializable]
public enum FSDeployableAlert : byte
{
    LowAmmo,
    Spent,
    Lost,
}

// Sent only to the engineer who placed the sentry, so they hear about it before the line does.
[Serializable, NetSerializable]
public sealed class FSDeployableAlertEvent(NetEntity target, NetCoordinates at, FSDeployableAlert alert, string name) : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly NetCoordinates At = at;
    public readonly FSDeployableAlert Alert = alert;
    public readonly string Name = name;
}
