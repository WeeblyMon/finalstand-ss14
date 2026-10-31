using Content.Shared._FinalStand.Titles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Titles;

[ByRefEvent]
public readonly record struct FSTitleEarnedEvent(NetUserId UserId, ProtoId<FSTitlePrototype> Title);
