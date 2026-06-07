import math

def round50(x):
    return int(round(x / 50) * 50)

def clamp_val(x, lo, hi):
    return max(lo, min(hi, x))

# Tier percentages by maxLevel
tier = {1: 0.15, 2: 0.25, 3: 0.40, 4: 0.60, 5: 0.75}

# Category multipliers
cat_mult = {
    'Stat': 1.0,
    'Effect': 1.75,
    'Identity': 3.0
}

STAT = 'Stat'
EFF = 'Effect'
IDENT = 'Identity'

type_cat = {
    'FireRate': STAT, 'Accuracy': STAT, 'AngleMax': STAT, 'MagazineSize': STAT,
    'ReloadSpeed': STAT, 'Range': STAT, 'SelfChargeSpeed': STAT, 'Recoil': STAT,
    'CritChance': STAT, 'CritDamage': STAT, 'MoneyGainBonus': STAT, 'MovementSpeed': STAT,
    'AttackSpeed': STAT, 'MagEfficiency': STAT, 'PelletCount': STAT, 'Pierce': STAT,
    'Damage': STAT, 'SpawnItem': STAT,
    # Effect
    'Knockback': EFF, 'Slowing': EFF, 'Suppression': EFF, 'APRounds': EFF,
    'ArmorShred': EFF, 'Bleed': EFF, 'SetOnFire': EFF, 'FlechetteRounds': EFF,
    'Scrapshot': EFF, 'Execution': EFF, 'BeamChaining': EFF, 'LifeSteal': EFF,
    'StaminaSteal': EFF, 'PulseCascade': EFF, 'SplinterImpact': EFF, 'ExplosiveShot': EFF,
    'ArmorShredStacking': EFF,
    # Identity
    'OverchargeShot': IDENT, 'SlamFire': IDENT, 'Overkill': IDENT, 'WarTorn': IDENT,
    'Prismatic': IDENT, 'FrequencyShift': IDENT,
    'Akimbo': 'Akimbo',
}

def compute_baseCost(price, maxLevel, upg_type):
    cap = price * 0.25
    floor_ = 50

    if upg_type == 'Akimbo':
        raw = price * 0.80
        result = round50(raw)
        result = max(result, 50)
        return result, 'AKIMBO', False, False

    cat = type_cat.get(upg_type, STAT)
    t = tier[maxLevel]
    cm = cat_mult[cat]

    raw = price * t * cm
    capped = False
    floored = False

    if raw > cap:
        raw = cap
        capped = True
    if raw < floor_:
        raw = floor_
        floored = True

    # When capped: floor to nearest $50 below cap so we never exceed it
    if capped:
        result = int(math.floor(raw / 50) * 50)
    else:
        result = round50(raw)

    if result < 50:
        result = 50
        floored = True

    return result, cat, capped, floored

weapons = [
    ('Viper', 3000, [
        ('viper-hair-trigger', 'Hair Trigger', 'FireRate', 5),
        ('viper-extended-mag', 'Extended Mag', 'SpawnItem', 5),
        ('viper-recoil-pad', 'Recoil Pad', 'Accuracy', 5),
        ('viper-akimbo', 'Akimbo', 'Akimbo', 1),
        ('viper-hollow-point', 'Hollow Point', 'Slowing', 1),
        ('viper-recoil-comp', 'Recoil Compensator', 'Recoil', 1),
    ]),
    ('Mateba', 1500, [
        ('mateba-hair-trigger', 'Hair Trigger', 'FireRate', 5),
        ('mateba-ammo-box', 'Ammo Box', 'SpawnItem', 5),
        ('mateba-steady-aim', 'Steady Aim', 'Accuracy', 5),
        ('mateba-dead-eye', 'Dead Eye', 'CritChance', 5),
        ('mateba-akimbo', 'Akimbo', 'Akimbo', 1),
        ('mateba-critical-mass', 'Critical Mass', 'CritDamage', 5),
    ]),
    ('Flintlock Pistol', 500, [
        ('flintlock-extra-load', 'Extra Load', 'MagazineSize', 5),
        ('flintlock-ball-and-powder', 'Ball and Powder', 'SpawnItem', 5),
        ('flintlock-steady-hand', 'Steady Hand', 'Accuracy', 5),
        ('flintlock-akimbo', 'Akimbo', 'Akimbo', 1),
        ('flintlock-explosive-shot', 'Explosive Shot', 'ExplosiveShot', 3),
        ('flintlock-payday', 'Payday', 'MoneyGainBonus', 4),
    ]),
    ('N1984', 9000, [
        ('n1984-hair-trigger', 'Hair Trigger', 'FireRate', 5),
        ('n1984-extended-mag', 'Extended Mag', 'SpawnItem', 5),
        ('n1984-recoil-pad', 'Recoil Pad', 'Accuracy', 5),
        ('n1984-akimbo', 'Akimbo', 'Akimbo', 1),
        ('n1984-ap-rounds', 'AP Rounds', 'APRounds', 1),
        ('n1984-long-barrel', 'Long Barrel', 'Range', 3),
        ('n1984-penetrator', 'Penetrator', 'Pierce', 4),
    ]),
    ('Adv. Laser Pistol', 18000, [
        ('advlaser-overcharge-coil', 'Overcharge Coil', 'FireRate', 5),
        ('advlaser-focusing-lens', 'Focusing Lens', 'Accuracy', 5),
        ('advlaser-rapid-charge', 'Rapid Charge', 'SelfChargeSpeed', 3),
        ('advlaser-ignite', 'Incendiary Beam', 'SetOnFire', 1),
        ('advlaser-chain-beam', 'Chain Beam', 'BeamChaining', 3),
    ]),
    ('Energy Magnum', 30000, [
        ('energymagnum-overcharge-coil', 'Overcharge Coil', 'FireRate', 5),
        ('energymagnum-focusing-crystal', 'Focusing Crystal', 'Accuracy', 5),
        ('energymagnum-rapid-charge', 'Rapid Charge', 'SelfChargeSpeed', 3),
        ('energymagnum-stopping-power', 'Stopping Power', 'Knockback', 3),
        ('energymagnum-penetrator', 'Penetrator', 'Pierce', 4),
    ]),
    ('Atreides', 5000, [
        ('atreides-akimbo', 'Akimbo', 'Akimbo', 1),
        ('atreides-hair-trigger', 'Hair Trigger', 'FireRate', 5),
        ('atreides-recoil-pad', 'Recoil Pad', 'Accuracy', 5),
        ('atreides-extended-mag', 'Extended Mag', 'MagazineSize', 5),
        ('atreides-speed-loader', 'Speed Loader', 'ReloadSpeed', 5),
        ('atreides-stamina-steal', 'Stamina Steal', 'StaminaSteal', 3),
    ]),
    ('WT550', 9000, [
        ('wt550-akimbo', 'Akimbo', 'Akimbo', 1),
        ('wt550-hair-trigger', 'Hair Trigger', 'FireRate', 5),
        ('wt550-speed-loader', 'Speed Loader', 'ReloadSpeed', 5),
        ('wt550-dead-eye', 'Dead Eye', 'CritChance', 5),
        ('wt550-fleet-foot', 'Fleet Foot', 'MovementSpeed', 3),
    ]),
    ('Drozd', 14000, [
        ('drozd-penetrator', 'Penetrator', 'Pierce', 4),
        ('drozd-hair-trigger', 'Hair Trigger', 'FireRate', 5),
        ('drozd-speed-loader', 'Speed Loader', 'ReloadSpeed', 5),
        ('drozd-dead-eye', 'Dead Eye', 'CritChance', 5),
        ('drozd-shredder', 'Shredder', 'ArmorShred', 5),
    ]),
    ('C-20r', 20000, [
        ('c20r-lifesteal', 'Life Steal', 'LifeSteal', 4),
        ('c20r-akimbo', 'Akimbo', 'Akimbo', 1),
        ('c20r-hair-trigger', 'Hair Trigger', 'FireRate', 5),
        ('c20r-recoil-pad', 'Recoil Pad', 'Accuracy', 5),
        ('c20r-extended-mag', 'Extended Mag', 'MagazineSize', 5),
        ('c20r-speed-loader', 'Speed Loader', 'ReloadSpeed', 5),
    ]),
    ('Blunderbuss', 8000, [
        ('blunderbuss-damage', 'Powder Charge', 'Damage', 5),
        ('blunderbuss-reload-speed', 'Speed Loader', 'ReloadSpeed', 5),
        ('blunderbuss-extra-pellets', 'Spread Shot', 'PelletCount', 3),
        ('blunderbuss-stopping-power', 'Stopping Power', 'Knockback', 3),
        ('blunderbuss-scrapshot', 'Scrapshot', 'Scrapshot', 1),
        ('blunderbuss-dragonbreath', "Dragon's Breath", 'SetOnFire', 1),
    ]),
    ('Kammerer', 13000, [
        ('kammerer-damage', 'Powder Charge', 'Damage', 5),
        ('kammerer-reload-speed', 'Speed Loader', 'ReloadSpeed', 5),
        ('kammerer-tight-choke', 'Tight Choke', 'AngleMax', 3),
        ('kammerer-slamfire', 'Slam Fire', 'SlamFire', 1),
        ('kammerer-ap-slugs', 'AP Slugs', 'APRounds', 1),
        ('kammerer-bleed', 'Serrated Shot', 'Bleed', 3),
    ]),
    ('Bulldog', 35000, [
        ('bulldog-fire-rate', 'Hair Trigger', 'FireRate', 5),
        ('bulldog-drum-capacity', 'High-Cap Drum', 'MagazineSize', 3),
        ('bulldog-recoil-comp', 'Recoil Compensator', 'AngleMax', 3),
        ('bulldog-flechette', 'Flechette Rounds', 'FlechetteRounds', 1),
        ('bulldog-incendiary', 'Incendiary Shells', 'SetOnFire', 1),
        ('bulldog-akimbo', 'Akimbo', 'Akimbo', 1),
    ]),
    ('Double Barrel', 25000, [
        ('doublebar-damage', 'Powder Charge', 'Damage', 5),
        ('doublebar-reload-speed', 'Speed Loader', 'ReloadSpeed', 5),
        ('doublebar-explosive-slugs', 'Explosive Slugs', 'ExplosiveShot', 3),
        ('doublebar-knockback', 'Massive Knockback', 'Knockback', 2),
        ('doublebar-splinter', 'Splinter Impact', 'SplinterImpact', 1),
    ]),
    ('Hushpup', 18000, [
        ('hushpup-tight-pattern', 'Tight Pattern', 'Accuracy', 3),
        ('hushpup-extra-pellets', 'Frag Load', 'PelletCount', 3),
        ('hushpup-slowing', 'Slowing Shells', 'Slowing', 1),
        ('hushpup-reload-speed', 'Speed Loader', 'ReloadSpeed', 5),
        ('hushpup-damage', 'Powder Charge', 'Damage', 5),
        ('hushpup-fire-rate', 'Hair Trigger', 'FireRate', 3),
    ]),
    ('Energy Shotgun', 50000, [
        ('eshottie-extra-pellets', 'Wide Beam', 'PelletCount', 3),
        ('eshottie-tight-choke', 'Focusing Lens', 'AngleMax', 3),
        ('eshottie-incendiary', 'Incendiary Beam', 'SetOnFire', 1),
        ('eshottie-extended-cell', 'Extended Cell', 'MagazineSize', 3),
        ('eshottie-damage', 'High-Energy Cell', 'Damage', 5),
        ('eshottie-rapid-charge', 'Rapid Charge', 'SelfChargeSpeed', 3),
        ('eshottie-overcharge', 'Overcharge Shot', 'OverchargeShot', 1),
    ]),
    ('AKMS', 30000, [
        ('akms-damage', 'Heavier Load', 'Damage', 5),
        ('akms-fire-rate', 'Hair Trigger', 'FireRate', 3),
        ('akms-recoil', 'Reduced Recoil', 'Accuracy', 3),
        ('akms-incendiary', 'Incendiary Rounds', 'SetOnFire', 1),
        ('akms-battle-trance', 'Battle Trance', 'WarTorn', 3),
        ('akms-suppression', 'Suppression', 'Suppression', 2),
    ]),
    ('Lecter', 20000, [
        ('lecter-damage', 'Heavy Rounds', 'Damage', 5),
        ('lecter-recoil', 'Reduced Recoil', 'Accuracy', 3),
        ('lecter-ap', 'AP Rounds', 'APRounds', 1),
        ('lecter-reload', 'Speed Loader', 'ReloadSpeed', 5),
        ('lecter-overkill', 'Overkill', 'Overkill', 3),
        ('lecter-execution', 'Execution', 'Execution', 1),
    ]),
    ('Laser Carbine', 45000, [
        ('lcarbine-charge-speed', 'Capacitor Boost', 'SelfChargeSpeed', 3),
        ('lcarbine-accuracy', 'Focusing Lens', 'Accuracy', 3),
        ('lcarbine-chain', 'Chain Targets', 'BeamChaining', 3),
        ('lcarbine-prismatic', 'Prismatic', 'Prismatic', 3),
        ('lcarbine-mag-efficiency', 'Mag Efficiency', 'MagEfficiency', 3),
    ]),
    ('Pulse Rifle', 65000, [
        ('pulse-damage', 'Overloaded Cell', 'Damage', 5),
        ('pulse-fire-rate', 'Rapid Pulse', 'FireRate', 3),
        ('pulse-accuracy', 'Focusing Array', 'Accuracy', 3),
        ('pulse-charge-speed', 'Capacitor Boost', 'SelfChargeSpeed', 3),
        ('pulse-cascade', 'Pulse Cascade', 'PulseCascade', 1),
    ]),
]

print("=" * 80)
print("COMPUTED UPGRADE BASE COSTS")
print("=" * 80)

all_results = {}

for wname, price, upgrades in weapons:
    cap = price * 0.25
    print(f"\n## {wname} (${price:,}) -- cap=${cap:,.0f}")
    weapon_results = []
    for uid, uname, utype, maxlvl in upgrades:
        cost, cat, capped, floored = compute_baseCost(price, maxlvl, utype)
        flags = []
        if cat == 'AKIMBO':
            flags.append('AKIMBO')
        if capped:
            flags.append(f'CAPPED at ${int(cap)}')
        if floored:
            flags.append('FLOOR')
        flag_str = ' [' + ', '.join(flags) + ']' if flags else ''
        print(f"  {uid}: {uname} ({utype}/{cat}, maxLvl={maxlvl}) -> baseCost=${cost}{flag_str}")
        weapon_results.append((uid, uname, utype, cat, maxlvl, cost, capped, floored))
    all_results[wname] = (price, weapon_results)

print("\n\nDone.")
