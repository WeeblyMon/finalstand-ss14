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
    private static readonly Color CardHover = Color.FromHex("#201e1c");
    private static readonly Color LockedFill = Color.FromHex("#161214");
    private static readonly Color LockedEdge = Color.FromHex("#4a3b58");
    private static readonly Color MaxedFill = Color.FromHex("#1d1810");
    private static readonly Color MaxedEdge = Color.FromHex("#5c4a26");
    private static readonly Color CaptionDim = Color.FromHex("#6a6761");
    private static readonly Color DescText = Color.FromHex("#a9a59f");

    private Control BuildStatBar(string label, float fill, string numericText,
        Color? valueColor = null, string? valueTooltip = null)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 5,
        };

        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        head.AddChild(new Label
        {
            Text = label.ToUpperInvariant(),
            FontOverride = _fontCaption,
            FontColorOverride = FSUiPalette.TextMuted,
            HorizontalExpand = true,
        });

        var valueLabel = new Label
        {
            Text = numericText,
            FontOverride = _fontValue,
            FontColorOverride = valueColor ?? FSUiPalette.TextPrimary,
            MouseFilter = string.IsNullOrEmpty(valueTooltip) ? MouseFilterMode.Ignore : MouseFilterMode.Stop,
            ToolTip = valueTooltip,
        };
        head.AddChild(valueLabel);
        box.AddChild(head);

        var segBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        var segments = new PanelContainer[BarSegments];
        for (var i = 0; i < BarSegments; i++)
        {
            segments[i] = new PanelContainer { HorizontalExpand = true, SetHeight = 6 };
            segBox.AddChild(segments[i]);
        }
        box.AddChild(segBox);

        var refs = new StatBarRefs
        {
            Segments = segments,
            ValueLabel = valueLabel,
            BaseFill = fill,
            BaseText = numericText,
            BaseColor = valueColor,
        };
        _statBarRefs[label] = refs;
        ApplyStatBar(refs, fill, numericText, valueColor, false);

        return box;
    }

    private void RebuildCards()
    {
        ClearPreview();
        UpgradesContainer.RemoveAllChildren();
        if (_defs.Count == 0)
        {
            UpgradesContainer.AddChild(new Label { Text = "No upgrades available.", FontColorOverride = FSUiPalette.TextMuted });
            UpgradeCounterLabel.Text = "";
            return;
        }

        var shopClient = _entityManager.System<FSShopClientSystem>();
        var cardWidth = _columns > 0
            ? (UpgradesScroll.Width - 14f - (_columns - 1) * 10f) / _columns
            : MinCardWidth;

        var bought = 0;
        var total = 0;
        foreach (var def in _defs)
        {
            if (def.RequiresUpgrade != null && _levels.GetValueOrDefault(def.RequiresUpgrade, 0) <= 0)
                continue;

            string? lockedBy = null;
            if (def.RequiresResearch is { } node && !shopClient.IsResearchNodeUnlocked(node.Id))
                lockedBy = _proto.TryIndex(node, out var nodeProto) ? nodeProto.Name : node.Id;

            var discounted = def.DiscountResearch is { } discount && shopClient.IsResearchNodeUnlocked(discount.Id);
            UpgradesContainer.AddChild(BuildCard(def, _levels.GetValueOrDefault(def.Id, 0), lockedBy, discounted, cardWidth));

            if (def.IsStub)
                continue;
            bought += _levels.GetValueOrDefault(def.Id, 0);
            total += def.MaxLevel;
        }

        UpgradeCounterLabel.Text = $"{bought} of {total} levels bought";
    }

    private Control BuildCard(WeaponUpgradeDef def, int level, string? lockedBy, bool discounted, float width)
    {
        var maxed = level >= def.MaxLevel;
        var locked = lockedBy != null && !maxed;
        var cost = maxed ? 0 : def.LevelCost(level + 1, discounted);
        var afford = _credits >= cost;

        var alt = def.AltWhenUpgrade != null && _levels.GetValueOrDefault(def.AltWhenUpgrade, 0) > 0;
        var name = alt && def.AltName != null ? def.AltName : def.Name;
        var desc = alt && def.AltDescription != null ? def.AltDescription : def.Description;

        var card = new PanelContainer
        {
            HorizontalExpand = true,
            MinWidth = MinCardWidth,
            MouseFilter = MouseFilterMode.Pass,
        };
        var fill = locked ? LockedFill : FSUiPalette.BgSurface;
        var edge = locked ? LockedEdge : FSUiPalette.BgTrack;
        card.PanelOverride = Box(fill, edge, new Thickness(1), 12, 12);

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10 };

        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var iconColor = maxed ? FSUiPalette.Currency : locked ? FSUiPalette.StateResearch : FSUiPalette.TextPrimary;
        var iconBox = new PanelContainer
        {
            SetSize = new Vector2(32, 32),
            PanelOverride = Box(FSUiPalette.BgDeep, FSUiPalette.BorderNeutral, new Thickness(1)),
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
        head.AddChild(iconBox);
        head.AddChild(new Label
        {
            Text = name,
            FontOverride = _fontBold,
            FontColorOverride = FSUiPalette.TextPrimary,
            HorizontalExpand = true,
            ClipText = true,
            VerticalAlignment = VAlignment.Center,
        });
        head.AddChild(new Label
        {
            Text = $"L{level} / {def.MaxLevel}",
            FontOverride = _fontCaption,
            FontColorOverride = CaptionDim,
            VerticalAlignment = VAlignment.Center,
        });
        body.AddChild(head);

        var track = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 3 };
        var nextSegment = new PanelContainer();
        for (var i = 0; i < def.MaxLevel; i++)
        {
            var seg = new PanelContainer { HorizontalExpand = true, SetHeight = 6 };
            Color segColor;
            if (i < level)
                segColor = maxed ? FSUiPalette.Currency : FSUiPalette.TextPrimary;
            else if (i == level && !locked)
            {
                segColor = Color.FromHex("#4a3f2a");
                nextSegment = seg;
            }
            else
                segColor = FSUiPalette.BgTrack;
            seg.PanelOverride = new StyleBoxFlat { BackgroundColor = segColor };
            track.AddChild(seg);
        }
        body.AddChild(track);

        var next = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3, MinHeight = 40 };
        var caption = def.IsStub ? "NOT AVAILABLE YET"
            : maxed ? "FULLY UPGRADED"
            : locked ? "NEEDS RESEARCH"
            : level == 0 ? "ON UNLOCK"
            : "NEXT LEVEL";
        next.AddChild(new Label { Text = caption, FontOverride = _fontCaption, FontColorOverride = CaptionDim });

        if (!maxed && !def.IsStub && TryPreview(def, out var statKey, out _, out var newText)
            && _statBarRefs.TryGetValue(statKey, out var refs))
        {
            next.AddChild(new Label
            {
                Text = $"{refs.BaseText}  →  {newText}",
                FontOverride = _fontTitle,
                FontColorOverride = locked ? FSUiPalette.TextMuted : FSUiPalette.Currency,
            });
        }
        else
        {
            var text = new RichTextLabel { MaxWidth = Math.Max(120f, width - 26f) };
            text.SetMessage(FormattedMessage.FromMarkupPermissive(
                $"[color={DescText.ToHex()}]{FormattedMessage.EscapeText(desc)}[/color]"));
            next.AddChild(text);
        }
        body.AddChild(next);

        var button = new Button { MinHeight = 36, HorizontalExpand = true, ToolTip = desc };
        button.Label.FontOverride = _fontCaption;
        button.Label.HorizontalAlignment = HAlignment.Center;

        Color btnFill, btnEdge, btnText;
        if (def.IsStub)
        {
            button.Text = "COMING SOON";
            (btnFill, btnEdge, btnText) = (FSUiPalette.BgElevated, FSUiPalette.BgTrack, FSUiPalette.TextMuted);
        }
        else if (maxed)
        {
            button.Text = "MAXED";
            (btnFill, btnEdge, btnText) = (MaxedFill, MaxedEdge, FSUiPalette.Currency);
        }
        else if (locked)
        {
            button.Text = $"LOCKED · {lockedBy!.ToUpperInvariant()}";
            button.ToolTip = Loc.GetString("shop-upgrade-locked-research", ("node", lockedBy));
            (btnFill, btnEdge, btnText) = (Color.FromHex("#221c27"), LockedEdge, FSUiPalette.StateResearch);
        }
        else if (!_owned)
        {
            button.Text = $"{Money(cost)} · OWN IT FIRST";
            (btnFill, btnEdge, btnText) = (FSUiPalette.BgElevated, FSUiPalette.BgTrack, FSUiPalette.TextMuted);
        }
        else if (!afford)
        {
            button.Text = $"{Money(cost)} · {Money(cost - _credits)} SHORT";
            (btnFill, btnEdge, btnText) = (FSUiPalette.BgElevated, Color.FromHex("#3a2420"), FSUiPalette.StateNegative);
        }
        else
        {
            button.Text = $"UPGRADE · {Money(cost)}";
            (btnFill, btnEdge, btnText) = (FSUiPalette.Currency, Color.FromHex("#e3bd70"), FSUiPalette.BgDeep);
        }

        var buyable = !def.IsStub && !maxed && !locked && _owned && afford;
        button.Disabled = !buyable;
        button.StyleBoxOverride = Box(btnFill, btnEdge, new Thickness(1));
        button.Label.FontColorOverride = btnText;
        button.OnPressed += _ => OnUpgradePressed?.Invoke(def.Id);
        body.AddChild(button);

        card.AddChild(body);

        if (!maxed && !def.IsStub)
        {
            void Enter()
            {
                card.PanelOverride = Box(CardHover, locked ? LockedEdge : FSUiPalette.BorderSubtle, new Thickness(1), 12, 12);
                iconBox.PanelOverride = Box(FSUiPalette.BgDeep, FSUiPalette.Currency, new Thickness(1));
                icon.Modulate = FSUiPalette.Currency;
                if (!locked)
                    nextSegment.PanelOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.Currency };
                SetHoveredUpgrade(def);
            }

            void Exit()
            {
                card.PanelOverride = Box(fill, edge, new Thickness(1), 12, 12);
                iconBox.PanelOverride = Box(FSUiPalette.BgDeep, FSUiPalette.BorderNeutral, new Thickness(1));
                icon.Modulate = iconColor;
                if (!locked)
                    nextSegment.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#4a3f2a") };
                ClearPreview();
            }

            card.OnMouseEntered += _ => Enter();
            card.OnMouseExited += _ => Exit();
            button.OnMouseEntered += _ => Enter();
            button.OnMouseExited += _ => Exit();
        }

        return card;
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
        WeaponUpgradeType.GrenadeCapacity or WeaponUpgradeType.DeployableCapacity or WeaponUpgradeType.FuelCapacity
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
        _ => "upgrade",
    };

    private static string Capitalize(string text)
        => text.Length == 0 ? text : text.Substring(0, 1).ToUpper() + text.Substring(1);
}
