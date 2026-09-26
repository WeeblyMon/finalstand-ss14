using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Shared._FinalStand.Shop;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Shop.UI;

public sealed partial class WeaponShopWindow
{
    private static readonly ResPath IconRsi = new("/Textures/_FinalStand/Interface/Shop/upgrade_icons.rsi");
    private static readonly Color RowHover = Color.FromHex("#201e1c");
    private static readonly Color LockedFill = Color.FromHex("#161214");
    private static readonly Color LockedEdge = Color.FromHex("#4a3b58");
    private static readonly Color MaxedFill = Color.FromHex("#1d1810");
    private static readonly Color MaxedEdge = Color.FromHex("#5c4a26");
    private static readonly Color CaptionDim = Color.FromHex("#6a6761");
    private static readonly Color DescText = Color.FromHex("#a9a59f");
    private static readonly Color GoldEdge = Color.FromHex("#e3bd70");
    private const float ButtonColumn = 124f;

    private Control BuildStatBar(string label, float fill, string numericText,
        Color? valueColor = null, string? valueTooltip = null)
    {
        var tile = new PanelContainer { HorizontalExpand = true };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };

        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        head.AddChild(new Label
        {
            Text = label.ToUpperInvariant(),
            FontOverride = _fontCaption,
            FontColorOverride = FSUiPalette.TextMuted,
            HorizontalExpand = true,
        });
        var deltaLabel = new Label { FontOverride = _fontCaption, FontColorOverride = FSUiPalette.BgDeep, Margin = new Thickness(5, 0) };
        var deltaChip = new PanelContainer
        {
            Visible = false,
            PanelOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.Currency },
        };
        deltaChip.AddChild(deltaLabel);
        head.AddChild(deltaChip);
        box.AddChild(head);

        var valueLabel = new Label
        {
            Text = numericText,
            FontOverride = _fontTitle,
            MouseFilter = string.IsNullOrEmpty(valueTooltip) ? MouseFilterMode.Ignore : MouseFilterMode.Stop,
            ToolTip = valueTooltip,
        };
        box.AddChild(valueLabel);

        var segBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        var segments = new PanelContainer[BarSegments];
        for (var i = 0; i < BarSegments; i++)
        {
            segments[i] = new PanelContainer { HorizontalExpand = true, SetHeight = 4 };
            segBox.AddChild(segments[i]);
        }
        box.AddChild(segBox);
        tile.AddChild(box);

        var refs = new StatBarRefs
        {
            Tile = tile,
            Segments = segments,
            ValueLabel = valueLabel,
            DeltaChip = deltaChip,
            DeltaLabel = deltaLabel,
            BaseFill = fill,
            BaseText = numericText,
            BaseColor = valueColor,
        };
        _statBarRefs[label] = refs;
        ApplyStatBar(refs, fill, numericText, valueColor, false);

        return tile;
    }

    private void RebuildRows()
    {
        ClearPreview();
        UpgradesContainer.RemoveAllChildren();

        var visible = _defs.Where(d => d.RequiresUpgrade == null || _levels.GetValueOrDefault(d.RequiresUpgrade, 0) > 0).ToList();
        var total = visible.Where(d => !d.IsStub).Sum(d => d.MaxLevel);
        var bought = visible.Where(d => !d.IsStub).Sum(d => Math.Min(_levels.GetValueOrDefault(d.Id, 0), d.MaxLevel));
        RefreshProgress(bought, total);

        if (visible.Count == 0)
        {
            UpgradesContainer.AddChild(new Label { Text = "No upgrades available.", FontColorOverride = FSUiPalette.TextMuted });
            return;
        }

        var statUps = visible.Where(d => IsStatUpgrade(d.Type)).ToList();
        var special = visible.Where(d => !IsStatUpgrade(d.Type)).ToList();
        if (statUps.Count > 0)
            UpgradesContainer.AddChild(BuildGroup("STAT UPGRADES", "Hover to preview on the stat tiles", statUps));
        if (special.Count > 0)
            UpgradesContainer.AddChild(BuildGroup(statUps.Count > 0 ? "SPECIAL" : "UPGRADES", "", special));
    }

    private void RefreshProgress(int bought, int total)
    {
        ProgressLabel.Text = $"{bought} / {total}";
        ProgressBar.RemoveAllChildren();
        for (var i = 0; i < total; i++)
        {
            ProgressBar.AddChild(new PanelContainer
            {
                HorizontalExpand = true,
                SetHeight = 6,
                PanelOverride = new StyleBoxFlat { BackgroundColor = i < bought ? FSUiPalette.Currency : FSUiPalette.BgTrack },
            });
        }
    }

    private Control BuildGroup(string title, string hint, List<WeaponUpgradeDef> defs)
    {
        var group = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        header.AddChild(new Label { Text = title, FontOverride = _fontBold, FontColorOverride = FSUiPalette.TextMuted });
        header.AddChild(new PanelContainer
        {
            HorizontalExpand = true,
            SetHeight = 1,
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.BgTrack },
        });
        if (hint.Length > 0)
            header.AddChild(new Label { Text = hint, FontColorOverride = CaptionDim });
        group.AddChild(header);

        var shopClient = _entityManager.System<FSShopClientSystem>();
        foreach (var def in defs)
        {
            string? lockedBy = null;
            if (def.RequiresResearch is { } node && !shopClient.IsResearchNodeUnlocked(node.Id))
                lockedBy = _proto.TryIndex(node, out var nodeProto) ? nodeProto.Name : node.Id;

            var discounted = def.DiscountResearch is { } discount && shopClient.IsResearchNodeUnlocked(discount.Id);
            group.AddChild(BuildRow(def, _levels.GetValueOrDefault(def.Id, 0), lockedBy, discounted));
        }

        return group;
    }

    private Control BuildRow(WeaponUpgradeDef def, int level, string? lockedBy, bool discounted)
    {
        var maxed = level >= def.MaxLevel;
        var locked = lockedBy != null && !maxed;
        var cost = maxed ? 0 : def.LevelCost(level + 1, discounted);
        var afford = _credits >= cost;

        var alt = def.AltWhenUpgrade != null && _levels.GetValueOrDefault(def.AltWhenUpgrade, 0) > 0;
        var name = alt && def.AltName != null ? def.AltName : def.Name;
        var desc = alt && def.AltDescription != null ? def.AltDescription : def.Description;

        var fill = locked ? LockedFill : FSUiPalette.BgSurface;
        var edge = locked ? LockedEdge : FSUiPalette.BgTrack;
        var row = new PanelContainer { MouseFilter = MouseFilterMode.Pass, PanelOverride = Box(fill, edge, new Thickness(1), 12, 10) };
        var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 14 };

        var iconColor = maxed ? FSUiPalette.Currency : locked ? FSUiPalette.StateResearch : FSUiPalette.TextPrimary;
        var iconEdge = maxed ? MaxedEdge : FSUiPalette.BorderNeutral;
        var iconBox = new PanelContainer
        {
            SetSize = new Vector2(42, 42),
            VerticalAlignment = VAlignment.Center,
            PanelOverride = Box(maxed ? MaxedFill : RowHover, iconEdge, new Thickness(1)),
        };
        var icon = new TextureRect
        {
            Texture = _entityManager.System<SpriteSystem>().Frame0(new SpriteSpecifier.Rsi(IconRsi, UpgradeIcon(def.Type))),
            SetSize = new Vector2(24, 24),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Modulate = iconColor,
        };
        iconBox.AddChild(icon);
        line.AddChild(iconBox);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 5,
        };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        head.AddChild(new Label { Text = name, FontOverride = _fontBold, FontColorOverride = FSUiPalette.TextPrimary });

        PanelContainer? nextPip = null;
        if (def.MaxLevel <= 10)
        {
            var pips = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2, VerticalAlignment = VAlignment.Center };
            for (var i = 0; i < def.MaxLevel; i++)
            {
                var pip = new PanelContainer
                {
                    SetSize = new Vector2(12, 5),
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = i < level ? (maxed ? FSUiPalette.Currency : FSUiPalette.TextPrimary) : FSUiPalette.BgTrack,
                    },
                };
                if (i == level && !locked)
                    nextPip = pip;
                pips.AddChild(pip);
            }
            head.AddChild(pips);
        }
        head.AddChild(new Label { Text = $"{level}/{def.MaxLevel}", FontOverride = _fontCaption, FontColorOverride = CaptionDim, VerticalAlignment = VAlignment.Center });
        text.AddChild(head);

        var descLabel = new RichTextLabel { MaxWidth = Math.Max(160f, _rowsWidth - 42f - ButtonColumn - 80f) };
        descLabel.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color={FSUiPalette.TextMuted.ToHex()}]{FormattedMessage.EscapeText(desc)}[/color]"));
        text.AddChild(descLabel);
        if (locked)
        {
            text.AddChild(new Label
            {
                Text = $"Unlocks with research: {lockedBy}",
                FontOverride = _fontCaption,
                FontColorOverride = FSUiPalette.StateResearch,
            });
        }
        line.AddChild(text);

        var action = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SetWidth = ButtonColumn,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 3,
        };

        var (button, sub, subColor) = BuildBuyButton(def, level, maxed, locked, lockedBy, cost, afford);
        action.AddChild(button);
        if (sub.Length > 0)
        {
            action.AddChild(new Label
            {
                Text = sub,
                FontOverride = _fontCaption,
                FontColorOverride = subColor,
                HorizontalAlignment = HAlignment.Center,
            });
        }
        line.AddChild(action);
        row.AddChild(line);

        if (!maxed && !def.IsStub)
        {
            void Enter()
            {
                row.PanelOverride = Box(RowHover, locked ? LockedEdge : FSUiPalette.BorderSubtle, new Thickness(1), 12, 10);
                iconBox.PanelOverride = Box(RowHover, FSUiPalette.Currency, new Thickness(1));
                icon.Modulate = FSUiPalette.Currency;
                if (nextPip != null)
                    nextPip.PanelOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.Currency };
                SetHoveredUpgrade(def);
            }

            void Exit()
            {
                row.PanelOverride = Box(fill, edge, new Thickness(1), 12, 10);
                iconBox.PanelOverride = Box(RowHover, iconEdge, new Thickness(1));
                icon.Modulate = iconColor;
                if (nextPip != null)
                    nextPip.PanelOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.BgTrack };
                ClearPreview();
            }

            row.OnMouseEntered += _ => Enter();
            row.OnMouseExited += _ => Exit();
            button.OnMouseEntered += _ => Enter();
            button.OnMouseExited += _ => Exit();
        }

        return row;
    }

    private (Control Button, string Sub, Color SubColor) BuildBuyButton(WeaponUpgradeDef def, int level,
        bool maxed, bool locked, string? lockedBy, int cost, bool afford)
    {
        if (maxed)
        {
            var badge = new PanelContainer { MinHeight = 38, PanelOverride = Box(MaxedFill, MaxedEdge, new Thickness(1)) };
            badge.AddChild(new Label
            {
                Text = "MAXED",
                FontOverride = _fontBold,
                FontColorOverride = FSUiPalette.Currency,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            });
            return (badge, "", CaptionDim);
        }

        var button = new Button { MinHeight = 38, ToolTip = def.Description };
        button.Label.FontOverride = _fontBold;
        button.Label.HorizontalAlignment = HAlignment.Center;
        button.OnPressed += _ => OnUpgradePressed?.Invoke(def.Id);

        Color btnFill, btnEdge, btnText;
        string sub;
        var subColor = CaptionDim;
        button.Text = Money(cost);

        if (def.IsStub)
        {
            button.Text = "SOON";
            sub = "Not available yet";
            (btnFill, btnEdge, btnText) = (FSUiPalette.BgElevated, FSUiPalette.BgTrack, FSUiPalette.TextMuted);
        }
        else if (locked)
        {
            button.Text = "LOCKED";
            button.ToolTip = Loc.GetString("shop-upgrade-locked-research", ("node", lockedBy!));
            sub = "Needs research";
            subColor = FSUiPalette.StateResearch;
            (btnFill, btnEdge, btnText) = (Color.FromHex("#221c27"), LockedEdge, FSUiPalette.StateResearch);
        }
        else if (!_owned)
        {
            sub = "Own it first";
            (btnFill, btnEdge, btnText) = (FSUiPalette.BgElevated, FSUiPalette.BgTrack, FSUiPalette.TextMuted);
        }
        else if (!afford)
        {
            sub = $"{Money(cost - _credits)} short";
            subColor = FSUiPalette.StateNegative;
            (btnFill, btnEdge, btnText) = (FSUiPalette.BgElevated, FSUiPalette.BgTrack, FSUiPalette.TextMuted);
        }
        else
        {
            sub = level == 0 ? "Unlock" : $"To level {level + 1}";
            (btnFill, btnEdge, btnText) = (FSUiPalette.Currency, GoldEdge, FSUiPalette.BgDeep);
        }

        button.Disabled = def.IsStub || locked || !_owned || !afford;
        button.StyleBoxOverride = Box(btnFill, btnEdge, new Thickness(1));
        button.Label.FontColorOverride = btnText;
        return (button, sub, subColor);
    }

    // Upgrades whose next level can be previewed on a stat tile.
    private static bool IsStatUpgrade(WeaponUpgradeType type) => type is WeaponUpgradeType.Damage
        or WeaponUpgradeType.FireRate or WeaponUpgradeType.Accuracy or WeaponUpgradeType.AngleMax
        or WeaponUpgradeType.MagazineSize or WeaponUpgradeType.CritChance or WeaponUpgradeType.CritDamage;

    // "+30" from "240 RPM" -> "270 RPM": compares the leading number of each value text.
    private static string Delta(string from, string to)
    {
        if (!TryLeadingNumber(from, out var a) || !TryLeadingNumber(to, out var b))
            return "NEW";
        var d = b - a;
        var text = Math.Abs(d % 1f) < 0.001f ? d.ToString("0", CultureInfo.InvariantCulture) : d.ToString("0.0#", CultureInfo.InvariantCulture);
        return d > 0 ? "+" + text : text;
    }

    private static bool TryLeadingNumber(string text, out float value)
    {
        var end = 0;
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.' || text[end] == ','))
            end++;
        return float.TryParse(text[..end].Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string UpgradeIcon(WeaponUpgradeType type) => type switch
    {
        WeaponUpgradeType.Damage or WeaponUpgradeType.SentryDamage or WeaponUpgradeType.LandmineDamage
            or WeaponUpgradeType.Overkill or WeaponUpgradeType.Execution or WeaponUpgradeType.ExecutionShot
            or WeaponUpgradeType.OverchargeShot or WeaponUpgradeType.OverloadRound => "damage",
        WeaponUpgradeType.FireRate or WeaponUpgradeType.SentryFireRate or WeaponUpgradeType.FullAuto
            or WeaponUpgradeType.SlamFire or WeaponUpgradeType.Overclocked or WeaponUpgradeType.Barrage
            or WeaponUpgradeType.MarksmansRhythm => "firerate",
        WeaponUpgradeType.MagazineSize or WeaponUpgradeType.SentryAmmo or WeaponUpgradeType.SpawnItem
            or WeaponUpgradeType.MagEfficiency or WeaponUpgradeType.AmmoBoxUses => "magazine",
        WeaponUpgradeType.Accuracy or WeaponUpgradeType.AngleMax or WeaponUpgradeType.HomingBolts => "accuracy",
        WeaponUpgradeType.APRounds or WeaponUpgradeType.ArmorShred or WeaponUpgradeType.ShapedCharge => "armorpierce",
        WeaponUpgradeType.Range or WeaponUpgradeType.SentryRange or WeaponUpgradeType.TeslaArcRange => "range",
        WeaponUpgradeType.Pierce or WeaponUpgradeType.ChargePierce or WeaponUpgradeType.FlechetteRounds => "pierce",
        WeaponUpgradeType.Knockback or WeaponUpgradeType.Suppression => "knockback",
        WeaponUpgradeType.Radius or WeaponUpgradeType.ExplosiveShot or WeaponUpgradeType.GrenadeBlastBonus
            or WeaponUpgradeType.GrenadeEffectRadius or WeaponUpgradeType.LandmineHighExplosive
            or WeaponUpgradeType.Aftershock or WeaponUpgradeType.SplinterImpact or WeaponUpgradeType.FractureRounds
            or WeaponUpgradeType.RadiationCoating or WeaponUpgradeType.VaporiseWeakMob => "blast",
        WeaponUpgradeType.GrenadeCapacity or WeaponUpgradeType.FuelCapacity
            or WeaponUpgradeType.LandmineDetonations => "capacity",
        WeaponUpgradeType.GrenadeRegen or WeaponUpgradeType.DeployableRegen or WeaponUpgradeType.SelfChargeSpeed
            or WeaponUpgradeType.AmmoBoxSpeed or WeaponUpgradeType.BounceRefund or WeaponUpgradeType.FuelEfficiency => "regen",
        WeaponUpgradeType.StunOnHit or WeaponUpgradeType.ConcussionClub or WeaponUpgradeType.GrenadeStunDuration
            or WeaponUpgradeType.GrenadeImpactFuse or WeaponUpgradeType.ChargeSpeed or WeaponUpgradeType.PulseCascade
            or WeaponUpgradeType.Resonance or WeaponUpgradeType.Prismatic => "bolt",
        WeaponUpgradeType.GrenadeCluster or WeaponUpgradeType.ClusterBarrage or WeaponUpgradeType.PelletCount
            or WeaponUpgradeType.Scrapshot or WeaponUpgradeType.Multishot or WeaponUpgradeType.GravitonCore
            or WeaponUpgradeType.GrenadeSingularity => "cluster",
        WeaponUpgradeType.CritChance or WeaponUpgradeType.CritDamage or WeaponUpgradeType.CritVsStunned
            or WeaponUpgradeType.CritVsBurning or WeaponUpgradeType.FlintlockCritSynergy or WeaponUpgradeType.PointBlankCrit
            or WeaponUpgradeType.BounceCrit => "crit",
        WeaponUpgradeType.SetOnFire or WeaponUpgradeType.GrenadeBurnDuration or WeaponUpgradeType.WhileBurningBuff => "fire",
        WeaponUpgradeType.MoneyGainBonus or WeaponUpgradeType.MoneyPerHit => "money",
        WeaponUpgradeType.LifeSteal or WeaponUpgradeType.ShieldVampire or WeaponUpgradeType.StaminaSteal => "heart",
        WeaponUpgradeType.Slowing or WeaponUpgradeType.GrenadeBaitDuration => "slow",
        WeaponUpgradeType.BeamChaining or WeaponUpgradeType.BounceCount or WeaponUpgradeType.BounceRetention => "chain",
        WeaponUpgradeType.FireResist or WeaponUpgradeType.WielderResistance or WeaponUpgradeType.IronBeast
            or WeaponUpgradeType.ShieldDurability or WeaponUpgradeType.Thorns => "shield",
        WeaponUpgradeType.MovementSpeed or WeaponUpgradeType.AttackSpeed or WeaponUpgradeType.ReloadSpeed
            or WeaponUpgradeType.SpeedLoader or WeaponUpgradeType.BattleTrance => "speed",
        WeaponUpgradeType.Bleed => "bleed",
        WeaponUpgradeType.DualWieldEnergySword => "melee",
        WeaponUpgradeType.DeployableCapacity => "deployable",
        _ => "upgrade",
    };

    private static string Capitalize(string text)
        => text.Length == 0 ? text : text.Substring(0, 1).ToUpper() + text.Substring(1);
}
