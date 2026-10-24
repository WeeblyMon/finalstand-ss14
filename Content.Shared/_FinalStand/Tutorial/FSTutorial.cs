using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Tutorial;

[Serializable, NetSerializable]
public enum FSTutorialTip : byte
{
    Welcome,
    Prep,
    FirstWave,
    Downed,
    Perks,
    DarkWave,
    BossWave,
    Runner,
    Armoured,
    Bloater,
    Flamethrower,
    Tesla,
    Devastator,
    Revenant,
    Controls,
    PerkShop,
    Grenades,
    CccAttacked,
    ReadyUp,
}

public static class FSTutorialText
{
    private static readonly EntProtoId CccProto = "FinalStandCCC";
    private static readonly EntProtoId WeaponShopProto = "FSWeaponShopViper";
    private static readonly EntProtoId PerkShopProto = "FSPerkShop";
    private static readonly EntProtoId AmmoBoxProto = "WaveAmmoBox";
    private static readonly EntProtoId GrenadeProto = "FSGrenadeFrag";
    private static readonly EntProtoId DefibProto = "FSEmergencyDefibrillator";
    private static readonly EntProtoId LanternProto = "FlashlightLantern";
    private static readonly EntProtoId GiantProto = "FSZombieGiant";
    private static readonly EntProtoId RunnerProto = "FSZombieRunner";
    private static readonly EntProtoId ArmouredProto = "FSZombieArmoured";
    private static readonly EntProtoId BloaterProto = "FSZombieBloater";
    private static readonly EntProtoId FlamethrowerProto = "FSZombieFlamethrower";
    private static readonly EntProtoId TeslaProto = "FSZombieTesla";
    private static readonly EntProtoId DevastatorProto = "FSZombieDevastator";
    private static readonly EntProtoId RevenantProto = "FSZombieRevenant";

    // Declared after the ids above: static initialisers run in text order.
    private static readonly Dictionary<EntProtoId, FSTutorialTip> EnemyTips = new()
    {
        [RunnerProto] = FSTutorialTip.Runner,
        [ArmouredProto] = FSTutorialTip.Armoured,
        [BloaterProto] = FSTutorialTip.Bloater,
        [FlamethrowerProto] = FSTutorialTip.Flamethrower,
        [TeslaProto] = FSTutorialTip.Tesla,
        [DevastatorProto] = FSTutorialTip.Devastator,
        [RevenantProto] = FSTutorialTip.Revenant,
    };

    public static LocId TitleLocId(FSTutorialTip tip) => $"fs-tutorial-{Key(tip)}-title";

    public static LocId BodyLocId(FSTutorialTip tip) => $"fs-tutorial-{Key(tip)}-body";

    public static string Key(FSTutorialTip tip) => tip switch
    {
        FSTutorialTip.Welcome => "welcome",
        FSTutorialTip.Prep => "prep",
        FSTutorialTip.FirstWave => "first-wave",
        FSTutorialTip.Downed => "downed",
        FSTutorialTip.Perks => "perks",
        FSTutorialTip.DarkWave => "dark-wave",
        FSTutorialTip.BossWave => "boss-wave",
        FSTutorialTip.Runner => "runner",
        FSTutorialTip.Armoured => "armoured",
        FSTutorialTip.Bloater => "bloater",
        FSTutorialTip.Flamethrower => "flamethrower",
        FSTutorialTip.Tesla => "tesla",
        FSTutorialTip.Devastator => "devastator",
        FSTutorialTip.Revenant => "revenant",
        FSTutorialTip.Controls => "controls",
        FSTutorialTip.PerkShop => "perk-shop",
        FSTutorialTip.Grenades => "grenades",
        FSTutorialTip.CccAttacked => "ccc-attacked",
        FSTutorialTip.ReadyUp => "ready-up",
        _ => "welcome",
    };

    public static EntProtoId? Icon(FSTutorialTip tip) => tip switch
    {
        FSTutorialTip.Welcome or FSTutorialTip.CccAttacked => CccProto,
        FSTutorialTip.Prep => WeaponShopProto,
        FSTutorialTip.PerkShop or FSTutorialTip.Perks => PerkShopProto,
        FSTutorialTip.FirstWave => AmmoBoxProto,
        FSTutorialTip.Grenades => GrenadeProto,
        FSTutorialTip.Downed => DefibProto,
        FSTutorialTip.DarkWave => LanternProto,
        FSTutorialTip.BossWave => GiantProto,
        FSTutorialTip.Runner => RunnerProto,
        FSTutorialTip.Armoured => ArmouredProto,
        FSTutorialTip.Bloater => BloaterProto,
        FSTutorialTip.Flamethrower => FlamethrowerProto,
        FSTutorialTip.Tesla => TeslaProto,
        FSTutorialTip.Devastator => DevastatorProto,
        FSTutorialTip.Revenant => RevenantProto,
        _ => (EntProtoId?) null,
    };

    public static FSTutorialTip? ForEnemy(EntProtoId prototype)
        => EnemyTips.TryGetValue(prototype, out var tip) ? tip : null;
}

[Serializable, NetSerializable]
public sealed class FSTutorialTipEvent : EntityEventArgs
{
    public FSTutorialTip Tip;
}
