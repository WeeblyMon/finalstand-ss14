using Content.Client._FinalStand.Stylesheets;
using Content.Shared._FinalStand.Shop;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Shop.UI;

public sealed partial class WeaponShopWindow
{
    // One level of def, applied to the stat it moves: which bar, its new fill, and its new value text.
    private bool TryPreview(WeaponUpgradeDef def, out string stat, out float fill, out string text)
    {
        stat = "";
        fill = 0f;
        text = "";
        if (!_rangedCache.Valid)
            return false;

        switch (def.Type)
        {
            case WeaponUpgradeType.Damage when _rangedCache.IsRanged:
            {
                var newDmg = _rangedCache.BaseDmgPerShot * (_rangedCache.DamageMultiplier + def.ValuePerLevel) * _rangedCache.AugDmgMult;
                var pellets = _rangedCache.BasePellets + _rangedCache.ExtraPellets;
                (stat, fill, text) = ("Damage", Math.Min(1f, newDmg * pellets / 100f),
                    pellets > 1 ? $"{newDmg:0.#}×{pellets}" : $"{newDmg:0.#}");
                return true;
            }
            case WeaponUpgradeType.FireRate when _rangedCache.IsRanged:
            {
                var newFr = (_rangedCache.UnboostedFireRate + def.ValuePerLevel) * _rangedCache.BulletStormMult;
                (stat, fill, text) = ("Fire Rate", Math.Min(1f, newFr / 10f), $"{(int)Math.Round(newFr * 60f)} RPM");
                return true;
            }
            case WeaponUpgradeType.Accuracy when _rangedCache.IsRanged:
            case WeaponUpgradeType.AngleMax when _rangedCache.IsRanged:
            {
                // The server replays one level through the same clamps the upgrade uses.
                if (!_entityManager.System<FSShopClientSystem>().NextLevelAccuracy.TryGetValue(def.Id, out var newStat))
                    return false;
                (stat, fill, text) = ("Accuracy", newStat / 100f, $"{newStat}");
                return true;
            }
            case WeaponUpgradeType.MagazineSize when _rangedCache.IsRanged && _rangedCache.Capacity >= 0:
            {
                var newCap = _rangedCache.Capacity + (int)Math.Round(def.ValuePerLevel);
                (stat, fill, text) = ("Capacity", Math.Min(1f, newCap / 30f), $"{newCap}");
                return true;
            }
            case WeaponUpgradeType.CritChance:
            {
                var chance = Math.Min(1f, _rangedCache.BaseCritChance + _rangedCache.CritChanceBonus + def.ValuePerLevel);
                (stat, fill, text) = ("Crit %", chance, $"{chance * 100f:F1}%");
                return true;
            }
            case WeaponUpgradeType.CritDamage:
            {
                var mult = _rangedCache.CritDamageMult + def.ValuePerLevel;
                (stat, fill, text) = ("Crit DMG", Math.Min(1f, (mult - 1f) / 2f), $"{mult:F1}x");
                return true;
            }
        }

        return false;
    }

    private void SetHoveredUpgrade(WeaponUpgradeDef def)
    {
        ClearPreview();
        if (!TryPreview(def, out var stat, out var fill, out var text) || !_statBarRefs.TryGetValue(stat, out var refs))
            return;

        ApplyStatBar(refs, fill, $"{refs.BaseText} → {text}", FSUiPalette.Currency, true);
    }

    private void ClearPreview()
    {
        foreach (var refs in _statBarRefs.Values)
            ApplyStatBar(refs, refs.BaseFill, refs.BaseText, refs.BaseColor, false);
    }

    // Segments the preview would add are drawn gold on top of the current fill.
    private static void ApplyStatBar(StatBarRefs refs, float fill, string text, Color? valueColor, bool preview)
    {
        var current = (int)Math.Round(Math.Clamp(refs.BaseFill, 0f, 1f) * refs.Segments.Length);
        var target = (int)Math.Round(Math.Clamp(fill, 0f, 1f) * refs.Segments.Length);
        var low = preview ? Math.Min(current, target) : target;
        var high = preview ? Math.Max(current, target) : target;

        for (var i = 0; i < refs.Segments.Length; i++)
        {
            var color = i < low ? FSUiPalette.TextPrimary
                : i < high ? FSUiPalette.Currency
                : FSUiPalette.BgTrack;
            refs.Segments[i].PanelOverride = new StyleBoxFlat { BackgroundColor = color };
        }

        refs.ValueLabel.Text = text;
        refs.ValueLabel.FontColorOverride = valueColor ?? FSUiPalette.TextPrimary;
    }
}
