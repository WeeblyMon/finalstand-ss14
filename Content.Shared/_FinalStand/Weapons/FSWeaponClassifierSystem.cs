using Content.Shared.Item;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Weapons;

// The single place that decides what kind of weapon an entity is, for the systems that apply research.
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

public sealed partial class FSWeaponClassifierSystem : EntitySystem
{
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public const int LargeWeight = 8;
    public const int HugeWeight = 16;

    private static readonly ProtoId<TagPrototype> BallisticTag = "WeaponGunBallistic";
    private static readonly ProtoId<TagPrototype> EnergyTag = "WeaponGunEnergy";
    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";

    public const string L6Proto = "FSWeaponLightMachineGunL6";
    public const string HydraProto = "WeaponLauncherHydraFS";
    public const string RpgProto = "FSWeaponLauncherRocket";
    public const string XrayProto = "WeaponXrayCannonFS";
    public const string TeslaProto = "WeaponTeslaGunFS";
    public const string HarvesterProto = "WeaponHarvesterFS";

    private static readonly HashSet<string> SemiAutoRifleProtos =
    [
        "WeaponSniperMosin",
        "WeaponRifleEstoc",
        "WeaponSniperHristov",
    ];

    public bool IsPistol(EntityUid uid)
    {
        return HasComp<GunComponent>(uid)
            && !HasComp<WieldableComponent>(uid)
            && TryComp<ItemComponent>(uid, out var item)
            && _item.GetSizePrototype(item.Size).Weight < LargeWeight;
    }

    public bool IsTwoHandedGun(EntityUid uid)
    {
        return HasComp<GunComponent>(uid) && HasComp<WieldableComponent>(uid);
    }

    public bool IsSemiAutoRifle(EntityUid uid)
    {
        return PrototypeId(uid) is { } id && SemiAutoRifleProtos.Contains(id);
    }

    public FSWeaponKind Classify(EntityUid uid)
    {
        var protoId = PrototypeId(uid);

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

    // A weapon can be gone by the time its bullet lands; that's no kind at all rather than an exception.
    private string? PrototypeId(EntityUid uid)
    {
        return TryComp(uid, out MetaDataComponent? meta) ? meta.EntityPrototype?.ID : null;
    }
}
