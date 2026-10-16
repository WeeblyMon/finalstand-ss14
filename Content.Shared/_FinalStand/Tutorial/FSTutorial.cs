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
}

public static class FSTutorialText
{
    public static string TitleLocId(FSTutorialTip tip) => $"fs-tutorial-{Key(tip)}-title";

    public static string BodyLocId(FSTutorialTip tip) => $"fs-tutorial-{Key(tip)}-body";

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
        _ => "welcome",
    };

    public static EntProtoId? Icon(FSTutorialTip tip) => tip switch
    {
        FSTutorialTip.Welcome => "FinalStandCCC",
        FSTutorialTip.CccAttacked => "FinalStandCCC",
        FSTutorialTip.Prep => "FSWeaponShopViper",
        FSTutorialTip.PerkShop => "FSPerkShop",
        FSTutorialTip.Perks => "FSPerkShop",
        FSTutorialTip.FirstWave => "WaveAmmoBox",
        FSTutorialTip.Grenades => "FSGrenadeFrag",
        FSTutorialTip.Downed => "FSEmergencyDefibrillator",
        FSTutorialTip.DarkWave => "FlashlightLantern",
        FSTutorialTip.BossWave => "FSZombieGiant",
        FSTutorialTip.Runner => "FSZombieRunner",
        FSTutorialTip.Armoured => "FSZombieArmoured",
        FSTutorialTip.Bloater => "FSZombieBloater",
        FSTutorialTip.Flamethrower => "FSZombieFlamethrower",
        FSTutorialTip.Tesla => "FSZombieTesla",
        FSTutorialTip.Devastator => "FSZombieDevastator",
        FSTutorialTip.Revenant => "FSZombieRevenant",
        _ => null,
    };

    public static FSTutorialTip? ForEnemy(string? prototype) => prototype switch
    {
        "FSZombieRunner" => FSTutorialTip.Runner,
        "FSZombieArmoured" => FSTutorialTip.Armoured,
        "FSZombieBloater" => FSTutorialTip.Bloater,
        "FSZombieFlamethrower" => FSTutorialTip.Flamethrower,
        "FSZombieTesla" => FSTutorialTip.Tesla,
        "FSZombieDevastator" => FSTutorialTip.Devastator,
        "FSZombieRevenant" => FSTutorialTip.Revenant,
        _ => null,
    };
}

[Serializable, NetSerializable]
public sealed class FSTutorialTipEvent : EntityEventArgs
{
    public FSTutorialTip Tip;
}
