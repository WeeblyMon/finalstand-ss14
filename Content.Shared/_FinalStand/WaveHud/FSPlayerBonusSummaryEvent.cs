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

    public bool Matches(in FSBonusCategory other)
    {
        if (!MathHelper.CloseToPercent(Percent, other.Percent) || Sources.Length != other.Sources.Length)
            return false;

        for (var i = 0; i < Sources.Length; i++)
        {
            if (Sources[i] != other.Sources[i])
                return false;
        }

        return true;
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
    public readonly FSBonusCategory CritChance;
    public readonly FSBonusCategory CritDamage;
    public readonly FSBonusCategory AttackSpeed;
    public readonly FSBonusCategory Resistance;
    public readonly FSBonusCategory MoveSpeed;

    public FSPlayerBonusSummaryEvent(FSBonusCategory gunDamage, FSBonusCategory fireRate,
        FSBonusCategory meleeDamage, FSBonusCategory explosiveDamage,
        FSBonusCategory reloadSpeed, FSBonusCategory magazineSize,
        FSBonusCategory critChance, FSBonusCategory critDamage, FSBonusCategory attackSpeed,
        FSBonusCategory resistance, FSBonusCategory moveSpeed)
    {
        CritChance = critChance;
        CritDamage = critDamage;
        AttackSpeed = attackSpeed;
        Resistance = resistance;
        MoveSpeed = moveSpeed;
        GunDamage = gunDamage;
        FireRate = fireRate;
        MeleeDamage = meleeDamage;
        ExplosiveDamage = explosiveDamage;
        ReloadSpeed = reloadSpeed;
        MagazineSize = magazineSize;
    }

    public bool Matches(FSPlayerBonusSummaryEvent other)
    {
        return GunDamage.Matches(other.GunDamage)
               && FireRate.Matches(other.FireRate)
               && MeleeDamage.Matches(other.MeleeDamage)
               && ExplosiveDamage.Matches(other.ExplosiveDamage)
               && ReloadSpeed.Matches(other.ReloadSpeed)
               && MagazineSize.Matches(other.MagazineSize)
               && CritChance.Matches(other.CritChance)
               && CritDamage.Matches(other.CritDamage)
               && AttackSpeed.Matches(other.AttackSpeed)
               && Resistance.Matches(other.Resistance)
               && MoveSpeed.Matches(other.MoveSpeed);
    }
}
