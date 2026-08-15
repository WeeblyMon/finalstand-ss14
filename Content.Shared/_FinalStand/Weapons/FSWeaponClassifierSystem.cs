// One place that decides what kind of weapon an entity is, for the systems that apply research.

using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Weapons;

public readonly record struct FSWeaponKind(
    bool Ballistic,
    bool Energy,
    bool Launcher,
    bool L6,
    bool Minigun,
    bool Hydra,
    bool Rpg,
    bool Xray,
    bool Tesla,
    bool Harvester)
{
    public bool HasGunTag => Ballistic || Energy || Launcher;
}

public sealed class FSWeaponClassifierSystem : EntitySystem
{
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> BallisticTag = "WeaponGunBallistic";
    private static readonly ProtoId<TagPrototype> EnergyTag = "WeaponGunEnergy";
    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";

    public const string L6Proto = "FSWeaponLightMachineGunL6";
    public const string HydraProto = "WeaponLauncherHydraFS";
    public const string RpgProto = "FSWeaponLauncherRocket";
    public const string XrayProto = "WeaponXrayCannonFS";
    public const string TeslaProto = "WeaponTeslaGunFS";
    public const string HarvesterProto = "WeaponHarvesterFS";

    public FSWeaponKind Classify(EntityUid uid)
    {
        var protoId = Prototype(uid)?.ID;

        return new FSWeaponKind(
            Ballistic: _tags.HasTag(uid, BallisticTag),
            Energy: _tags.HasTag(uid, EnergyTag),
            Launcher: _tags.HasTag(uid, LauncherTag),
            L6: protoId == L6Proto,
            Minigun: HasComp<FSMinigunComponent>(uid),
            Hydra: protoId == HydraProto,
            Rpg: protoId == RpgProto,
            Xray: protoId == XrayProto,
            Tesla: protoId == TeslaProto,
            Harvester: protoId == HarvesterProto);
    }
}
