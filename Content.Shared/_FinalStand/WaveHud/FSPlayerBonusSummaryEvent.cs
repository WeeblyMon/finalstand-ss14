using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public readonly struct FSBonusCategory
{
    public readonly float Percent;
    public readonly string[] Sources;

    public FSBonusCategory(float percent, string[] sources)
    {
        Percent = percent;
        Sources = sources;
    }
}

// Single-player-targeted snapshot of the wave-HUD "current bonuses" indicator.
[Serializable, NetSerializable]
public sealed class FSPlayerBonusSummaryEvent : EntityEventArgs
{
    public readonly FSBonusCategory GunDamage;
    public readonly FSBonusCategory FireRate;
    public readonly FSBonusCategory MeleeDamage;
    public readonly FSBonusCategory ExplosiveDamage;
    public readonly FSBonusCategory ReloadSpeed;
    public readonly FSBonusCategory MagazineSize;

    public FSPlayerBonusSummaryEvent(FSBonusCategory gunDamage, FSBonusCategory fireRate,
        FSBonusCategory meleeDamage, FSBonusCategory explosiveDamage,
        FSBonusCategory reloadSpeed, FSBonusCategory magazineSize)
    {
        GunDamage = gunDamage;
        FireRate = fireRate;
        MeleeDamage = meleeDamage;
        ExplosiveDamage = explosiveDamage;
        ReloadSpeed = reloadSpeed;
        MagazineSize = magazineSize;
    }
}
