using Content.Shared._FinalStand.Fortifications;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Fortifications;

// Everything poured into a structure, for one that gets torn down and rebuilt (a packed emplacement).
public readonly record struct FSUpgradeSnapshot(
    ProtoId<FSUpgradePrototype> Grade,
    ProtoId<FSUpgradePrototype>? Branch,
    List<ProtoId<FSUpgradePrototype>> Modules,
    ProtoId<FSUpgradePrototype>? Pending,
    int Poured);
