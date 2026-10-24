fs-buff-source-perk = Perk

fs-scavenger-ammo = +AMMO
fs-scavenger-heal = +{ $heal } HP
fs-scavenger-credits = +${ $credits }

fs-perk-shop-slot-empty = Slot { $slot }: empty
fs-perk-shop-slot-filled = Slot { $slot }: { $perk } (level { $level })
fs-perk-shop-category-owned = { $owned } of { $total } owned
fs-perk-shop-no-match = No perks match "{ $search }".
fs-perk-shop-tile-locked = { $perk } — not owned
fs-perk-shop-tile-level = { $perk } — level { $level } of { $max }
fs-perk-shop-pp = { $amount } PP

fs-perk-shop-select = Select a perk
fs-perk-shop-select-hint = Pick a perk from the list to see what each level does.
fs-perk-shop-not-owned = Not owned
fs-perk-shop-level-of = Level { $level } of { $max }
fs-perk-shop-max-level = MAX LEVEL
fs-perk-shop-unlock-cost = UNLOCK · { $cost } PP
fs-perk-shop-upgrade-cost = UPGRADE TO L{ $level } · { $cost } PP
fs-perk-shop-shortfall = You need { $amount } more PP.
fs-perk-shop-unequip = UNEQUIP FROM SLOT { $slot }
fs-perk-shop-equip = EQUIP TO NEXT FREE SLOT
fs-perk-shop-slots-full = ALL 6 SLOTS FULL
fs-perk-shop-level-short = L{ $level }
fs-perk-shop-ladder-owned = OWNED
fs-perk-shop-ladder-unlock = UNLOCK
fs-perk-shop-ladder-next = NEXT
fs-perk-shop-prestige-level = Prestige { $prestige } — Level { $level }
fs-perk-shop-prestige-bonus = Current XP Bonus: +{ $bonus }%
fs-perk-shop-prestige-requirement = Reach Level 50 to Prestige
fs-perk-shop-respec-title = Respec
fs-perk-shop-respec-heading = RESPEC ALL PERKS?
fs-perk-shop-respec-intro = You get [color={ $color }][bold]{ $refund } PP[/bold][/color] back and can rebuild from scratch.
fs-perk-shop-respec-reset = WILL BE RESET
fs-perk-shop-respec-reset-body = { $perks } perks go back to level 0 · { $slots } equipped slots are cleared
fs-perk-shop-respec-kept = KEPT
fs-perk-shop-respec-kept-loadouts = Your { $count } saved { $count ->
    [one] loadout
   *[other] loadouts
} — load one later to rebuild instantly
fs-perk-shop-respec-kept-no-loadouts = No saved loadouts yet
fs-perk-shop-respec-kept-points = Your { $points } unspent PP
fs-perk-shop-respec-cancel = CANCEL
fs-perk-shop-respec-save-first = SAVE TO LOADOUT { $loadout } FIRST
fs-perk-shop-respec-saved = SAVED TO LOADOUT { $loadout }
fs-perk-shop-respec-confirm = RESPEC · +{ $refund } PP
fs-perk-shop-chip = { $perk } — level { $level }
fs-perk-shop-prestige-confirm-title = Confirm Prestige
fs-perk-shop-prestige-confirm-reset = Prestige? Your level resets to 1.
fs-perk-shop-prestige-confirm-kept = Perks and points are kept.
fs-perk-shop-prestige-confirm-bonus = You gain +20% permanent XP bonus.
fs-perk-shop-prestige-confirm = Confirm
fs-perk-shop-prestige-cancel = Cancel

fs-perk-shop-category-offense = OFFENSE
fs-perk-shop-category-defense = DEFENSE
fs-perk-shop-category-utility = UTILITY
fs-perk-shop-category-economy = ECONOMY
fs-perk-shop-category-hybrid = HYBRID
fs-perk-shop-tab-perks = Perks
fs-perk-shop-tab-prestige = Prestige
fs-perk-shop-invested = { $amount } PP invested
fs-perk-shop-loadout-load = LOAD
fs-perk-shop-loadout-load-tooltip = Swap to this build ({ $perks } perks). Your current build is refunded first.
fs-perk-shop-loadout-empty = Nothing saved here
fs-perk-shop-loadout-save = SAVE
fs-perk-shop-loadout-save-empty = Your build is empty — nothing to save
fs-perk-shop-loadout-overwrite = Overwrite with your current build
fs-perk-shop-loadout-save-tooltip = Save your current build here
fs-perk-shop-title = Perk Shop
fs-perk-shop-equipped = EQUIPPED
fs-perk-shop-pp-unit = PP
fs-perk-shop-filter-all = ALL
fs-perk-shop-search = Search perks
fs-perk-shop-loadouts = LOADOUTS
fs-perk-shop-respec = RESPEC…
fs-perk-shop-prestige-button = ✦  PRESTIGE  ✦
fs-perk-shop-prestige-history = PRESTIGE HISTORY
fs-perk-shop-prestige-history-empty = No prestiges yet.
fs-perk-undying-triggered = Undying!

## Perk definitions (FSPerkDef)

fs-perk-stopping-power-name = Stopping Power
fs-perk-stopping-power-desc = Deal increased projectile damage. Does not apply to launchers.
fs-perk-stopping-power-level = +{ $value }% Damage

fs-perk-bullet-storm-name = Bullet Storm
fs-perk-bullet-storm-desc = Increase fire rate on all firearms.
fs-perk-bullet-storm-level = +{ $value }% Fire Rate

fs-perk-juggernaught-name = Juggernaught
fs-perk-juggernaught-desc = Take less damage from zombies.
fs-perk-juggernaught-level = +{ $value } Resistance

fs-perk-lightweight-name = Lightweight
fs-perk-lightweight-desc = Increases your movement speed.
fs-perk-lightweight-level = +{ $value }% Speed

fs-perk-profiteer-name = Profiteer
fs-perk-profiteer-desc = Increases the amount of money you earn.
fs-perk-profiteer-level = +{ $value }% Money

fs-perk-sword-and-shield-name = Sword and Shield
fs-perk-sword-and-shield-desc = Increases your melee damage and damage resistance while wielding a melee weapon.
fs-perk-sword-and-shield-level = +{ $value }% Damage / +{ $value2 } Resistance

fs-perk-death-aura-name = Death Aura
fs-perk-death-aura-desc = Kills grant stacks, increasing your damage by 2% per stack. Lose all stacks after 8s.
fs-perk-death-aura-level = +{ $value } Max Stacks

fs-perk-adrenaline-name = Adrenaline
fs-perk-adrenaline-desc = Killing an enemy grants unlimited stamina for a few seconds.
fs-perk-adrenaline-level = +{ $value }s Duration

fs-perk-speed-demon-name = Speed Demon
fs-perk-speed-demon-desc = Kills increase your movement speed up to 7 stacks. Lose 1 stack/s after 5s.
fs-perk-speed-demon-level = +{ $value }% Speed/Stack

fs-perk-rampage-name = Rampage
fs-perk-rampage-desc = Melee kills increase resistance, health regen, and speed. 5 stacks max.
fs-perk-rampage-level-1 = +3 Resist/+0.2 Regen/+2% Speed
fs-perk-rampage-level-2 = +6/+0.4/+4%
fs-perk-rampage-level-3 = +9/+0.6/+6%
fs-perk-rampage-level-4 = +12/+0.8/+8%

fs-perk-showstopper-name = Showstopper
fs-perk-showstopper-desc = Your style rank boosts melee damage. Melee kills at S rank or higher heal you.
fs-perk-showstopper-level = +{ $value }% Damage per Rank / +{ $value2 } HP per Kill

fs-perk-investor-name = Investor
fs-perk-investor-desc = At the end of each wave your money gains interest.
fs-perk-investor-level = +{ $value }% Return

fs-perk-mutual-fund-name = Mutual Fund
fs-perk-mutual-fund-desc = At the end of each wave your team's money gains interest.
fs-perk-mutual-fund-level = +{ $value }% Team Return

fs-perk-life-leech-name = Life Leech
fs-perk-life-leech-desc = Regenerate health after every zombie you kill.
fs-perk-life-leech-level = +{ $value } Health

fs-perk-untouchable-name = Untouchable
fs-perk-untouchable-desc = Automatically blocks one incoming hit. Charges refill after 30 seconds.
fs-perk-untouchable-level-1 = +1 Max Charge
fs-perk-untouchable-level-2 = +2 Max Charges
fs-perk-untouchable-level-3 = +3 Max Charges
fs-perk-untouchable-level-4 = +4 Max Charges

fs-perk-martyr-name = Martyr
fs-perk-martyr-desc = Going down triggers an explosion at your feet. 20 second cooldown.
fs-perk-martyr-level-1 = Small Explosion
fs-perk-martyr-level-2 = Medium Explosion
fs-perk-martyr-level-3 = Large Explosion
fs-perk-martyr-level-4 = Max Explosion

fs-perk-glass-cannon-name = Glass Cannon
fs-perk-glass-cannon-desc = Take 100% more damage, but deal more damage.
fs-perk-glass-cannon-level = +{ $value }% Damage

fs-perk-pacifist-name = Pacifist
fs-perk-pacifist-desc = Deal 25% less damage, but gain significant damage resistance.
fs-perk-pacifist-level = +{ $value } Resistance

fs-perk-field-medic-name = Field Medic
fs-perk-field-medic-desc = Bandages, ointments and medigun beams you apply heal more.
fs-perk-field-medic-level = +{ $value }% Healing

fs-perk-cargonian-name = Cargonian
fs-perk-cargonian-desc = Reduces the movement speed penalty from dragging bodies.
fs-perk-cargonian-level-1 = -33% Drag Penalty
fs-perk-cargonian-level-2 = -67% Drag Penalty
fs-perk-cargonian-level-3 = -100% Drag Penalty
fs-perk-cargonian-level-4 = -100% Drag Penalty

fs-perk-leg-breaker-name = Leg Breaker
fs-perk-leg-breaker-desc = Critical hits stagger enemies with stamina damage.
fs-perk-leg-breaker-level = { $value } Stamina Damage on Crit

fs-perk-back-breaker-name = Back Breaker
fs-perk-back-breaker-desc = Critical shots knock enemies back.
fs-perk-back-breaker-level-1 = +3 Knockback
fs-perk-back-breaker-level-2 = +6 Knockback
fs-perk-back-breaker-level-3 = +9 Knockback
fs-perk-back-breaker-level-4 = +12 Knockback

fs-perk-knockback-blast-name = Knockback Blast
fs-perk-knockback-blast-desc = Your shotgun shots knock enemies back.
fs-perk-knockback-blast-level-1 = +3 Knockback
fs-perk-knockback-blast-level-2 = +6 Knockback
fs-perk-knockback-blast-level-3 = +9 Knockback
fs-perk-knockback-blast-level-4 = +12 Knockback

fs-perk-deep-impact-name = Deep Impact
fs-perk-deep-impact-desc = Your shots pierce through enemies, halving damage with each one.
fs-perk-deep-impact-level-1 = +1 Pierce
fs-perk-deep-impact-level-2 = +2 Pierce
fs-perk-deep-impact-level-3 = +3 Pierce
fs-perk-deep-impact-level-4 = +4 Pierce

fs-perk-officer-name = Officer
fs-perk-officer-desc = Using a whistle near allies increases their damage for 8 seconds.
fs-perk-officer-level = +{ $value }% Ally Damage
fs-perk-officer-buff = Officer: +{ $value }% damage

fs-perk-harvester-tuning-name = Harvester Tuning
fs-perk-harvester-tuning-desc = Increases research points gained per Harvester hit.
fs-perk-harvester-tuning-level-1 = +1 RP/Hit
fs-perk-harvester-tuning-level-2 = +2 RP/Hit
fs-perk-harvester-tuning-level-3 = +3 RP/Hit
fs-perk-harvester-tuning-level-4 = +4 RP/Hit

fs-perk-combat-medic-name = Combat Medic
fs-perk-combat-medic-desc = Healing an ally grants you both increased damage for 15 seconds.
fs-perk-combat-medic-level = +{ $value }% Damage
fs-perk-combat-medic-buff = Combat Medic: +{ $value }% damage

fs-perk-ravage-name = Ravage
fs-perk-ravage-desc = Increases critical hit chance and attack speed with melee weapons.
fs-perk-ravage-level = +{ $value }% Crit Chance / +{ $value2 }% Attack Speed

fs-perk-critical-plus-name = Critical Plus
fs-perk-critical-plus-desc = Increases your critical hit damage multiplier.
fs-perk-critical-plus-level = +{ $value }% Crit Damage

fs-perk-implosion-name = Implosion
fs-perk-implosion-desc = Increases your explosive damage, but your explosions are 25% smaller.
fs-perk-implosion-level = +{ $value }% Explosive Damage

fs-perk-rifleman-name = Rifleman
fs-perk-rifleman-desc = Increases crit chance and crit damage with the Mosin, Estoc and Hristov.
fs-perk-rifleman-level = +{ $value }% Crit Chance/Damage

fs-perk-berserker-name = Berserker
fs-perk-berserker-desc = The lower your health, the more damage you deal. Halved for ranged damage.
fs-perk-berserker-level = Up to +{ $value }% Damage

fs-perk-undying-name = Undying
fs-perk-undying-desc = Going down lets you keep fighting with unlimited ammo, then you die.
fs-perk-undying-level = { $value }s Duration
fs-perk-undying-buff = Undying: unlimited ammo, then you die

fs-perk-man-on-fire-name = The Man On Fire
fs-perk-man-on-fire-desc = Reduces fire damage taken. At max level, burning slowly heals you.
fs-perk-man-on-fire-level = -{ $value }% Fire Damage

fs-perk-bandolier-name = Bandolier
fs-perk-bandolier-desc = Increases the magazine size of your guns.
fs-perk-bandolier-level = +{ $value }% Magazine Size

fs-perk-shadow-rounds-name = Shadow Rounds
fs-perk-shadow-rounds-desc = Shots have a chance to not consume ammo.
fs-perk-shadow-rounds-level = { $value }% Chance

fs-perk-bloodload-name = Bloodload
fs-perk-bloodload-desc = Melee kills increase your reload speed for 12 seconds.
fs-perk-bloodload-level = +{ $value }% Reload Speed
fs-perk-bloodload-buff = Bloodload: +{ $value }% reload speed

fs-perk-speedload-name = Speedload
fs-perk-speedload-desc = Increases your reload speed.
fs-perk-speedload-level = +{ $value }% Reload Speed

fs-perk-executioner-name = Executioner
fs-perk-executioner-desc = Deal increased damage to zombies below 35% health.
fs-perk-executioner-level = +{ $value }% Damage

fs-perk-shredder-name = Shredder
fs-perk-shredder-desc = Hits stack vulnerability on zombies, up to 10. Stacks fade 4s after the last hit.
fs-perk-shredder-level = +{ $value }% Damage Taken per Stack

fs-perk-specialised-killing-name = Specialised Killing
fs-perk-specialised-killing-desc = Deal increased damage to special zombies and bosses.
fs-perk-specialised-killing-level = +{ $value }% Damage

fs-perk-thorns-name = Thorns
fs-perk-thorns-desc = Kills grant stacks. Taking zombie damage spends one and reflects it back.
fs-perk-thorns-level = { $value } Stacks / { $value2 }% Reflect

fs-perk-static-discharge-name = Static Discharge
fs-perk-static-discharge-desc = Getting hit by a zombie stuns the zombies around you. Specials for half as long.
fs-perk-static-discharge-level = { $value } Tile Radius / { $value2 }s Cooldown

fs-perk-built-to-last-name = Built To Last
fs-perk-built-to-last-desc = Your barricades take less damage. You heal when one of them breaks.
fs-perk-built-to-last-level = -{ $value }% Barricade Damage / +{ $value2 } HP

fs-perk-technician-name = Technician
fs-perk-technician-desc = +1 stock and cap on every deployable, more for mines and barricades. Refills each wave.
fs-perk-technician-level-1 = +1 All Deployables
fs-perk-technician-level-2 = +2 Mines/Barricades / +1 Others
fs-perk-technician-level-3 = +3 Mines/Barricades / +1 Others
fs-perk-technician-level-4 = +4 Mines/Barricades / +1 Others

fs-perk-underdog-name = Underdog
fs-perk-underdog-desc = Gain damage and resistance for every zombie near you, up to 8.
fs-perk-underdog-level = +{ $value }% per Nearby Zombie

fs-perk-scavenger-name = Scavenger
fs-perk-scavenger-desc = Kills can drop a supply cache holding ammo, a heal or credits.
fs-perk-scavenger-level = { $value }% Drop Chance

fs-perk-concealed-carry-name = Concealed Carry
fs-perk-concealed-carry-desc = More crit chance with guns. Halved for large guns, none for huge ones.
fs-perk-concealed-carry-level = +{ $value }% Crit Chance

fs-perk-gunslinger-name = Gunslinger
fs-perk-gunslinger-desc = Pistols hit harder and fire faster. Two-handed guns get the reverse.
fs-perk-gunslinger-level = +{ $value }% Pistol Damage / +{ $value2 }% Pistol Fire Rate

fs-perk-lone-wolf-name = Lone Wolf
fs-perk-lone-wolf-desc = With no teammate within 10 tiles, deal more damage and regenerate health.
fs-perk-lone-wolf-level = +{ $value }% Damage / +{ $value2 } HP/s

fs-perk-bunkered-down-name = Bunkered Down
fs-perk-bunkered-down-desc = More damage and toughness within 12 tiles of the CCC, 10% less damage away from it.
fs-perk-bunkered-down-level = +{ $value }% Damage / +{ $value2 } Resistance

fs-perk-deadly-gear-name = Deadly Gear
fs-perk-deadly-gear-desc = Your sentries, HMG fire and landmines deal more damage.
fs-perk-deadly-gear-level = +{ $value }% Equipment Damage

fs-perk-ol-reliable-name = Ol' Reliable
fs-perk-ol-reliable-desc = Shotguns deal more damage for each of your sentries still standing, up to 5.
fs-perk-ol-reliable-level = +{ $value }% Shotgun Damage per Sentry

fs-perk-style-points-name = Style Points
fs-perk-style-points-desc = A gun left unfired for 5s builds up bonus damage. Firing it drains the bonus.
fs-perk-style-points-level = Up to +{ $value }% Damage

fs-perk-smoking-kills-name = Smoking Kills
fs-perk-smoking-kills-desc = Deal more damage while a lit cigarette, cigar or pipe is in your mouth.
fs-perk-smoking-kills-level = +{ $value }% Damage

fs-perk-wiseguy-name = Wiseguy
fs-perk-wiseguy-desc = Each wave you finish without going down raises your income. Going down resets it.
fs-perk-wiseguy-level = +{ $value }% Money/Wave (Max { $value2 }%)

fs-perk-looking-thin-name = Looking Thin
fs-perk-looking-thin-desc = Move faster with no armour on. Light armour halves it, heavy armour removes it.
fs-perk-looking-thin-level = +{ $value }% Speed

fs-perk-pack-mule-name = Pack Mule
fs-perk-pack-mule-desc = Slowdowns from armour, heavy guns, wounds and the like hit you less.
fs-perk-pack-mule-level = -{ $value }% Speed Penalties

fs-perk-aggravator-name = Aggravator
fs-perk-aggravator-desc = Kills stack +1% damage and draw nearby zombies to you. Lose a stack every 5s.
fs-perk-aggravator-level = +{ $value } Max Stacks

fs-perk-stunning-strikes-name = Stunning Strikes
fs-perk-stunning-strikes-desc = Riot shield hits can stun zombies for 1.2s. Specials: half as long, once per 10s.
fs-perk-stunning-strikes-level = { $value }% Stun Chance

fs-perk-bulwark-name = Bulwark
fs-perk-bulwark-desc = Tougher riot shield. Blocking draws zombies to you, melee kills mend the shield.
fs-perk-bulwark-level = +{ $value }% Shield Durability / +{ $value2 }% Mend per Kill

fs-perk-beacon-of-light-name = Beacon of Light
fs-perk-beacon-of-light-desc = While blocking with a riot shield, nearby allies stop bleeding and slowly heal.
fs-perk-beacon-of-light-level = { $value } Tile Radius / +{ $value2 } HP/s

fs-perk-medievalist-name = Medievalist
fs-perk-medievalist-desc = No guns or armour. Big melee damage and toughness. Locks in at wave start.
fs-perk-medievalist-level = +{ $value }% Melee Damage / +{ $value2 } Resistance

fs-perk-slush-fund-name = Slush Fund
fs-perk-slush-fund-desc = An extra share of what you earn goes into the Medical fund. Medical staff only.
fs-perk-slush-fund-level = +{ $value }% of Income to Fund

fs-perk-drunken-berserker-name = Drunken Berserker
fs-perk-drunken-berserker-desc = Drunk: more melee damage and fire rate, but worse aim and frailer. No alcohol poisoning.
fs-perk-drunken-berserker-level = +{ $value }% Melee Damage and Spread / +{ $value2 }% Fire Rate

fs-perk-explosive-engineering-name = Explosive Engineering
fs-perk-explosive-engineering-desc = Drunk: bigger blasts from your grenades and rockets, harder-hitting sentries.
fs-perk-explosive-engineering-level = +{ $value }% Blast Radius / +{ $value2 }% Sentry Damage

fs-perk-raging-alcoholic-name = Raging Alcoholic
fs-perk-raging-alcoholic-desc = No alcohol poisoning. The drunker you are, the faster you heal.
fs-perk-raging-alcoholic-level = Up to +{ $value } HP/s

fs-perk-wolf-pack-name = Wolf Pack
fs-perk-wolf-pack-desc = With a teammate within 6 tiles, reload faster and take less damage.
fs-perk-wolf-pack-level = +{ $value }% Reload Speed / +{ $value2 } Resistance

fs-starter-build-rifleman-name = Rifleman Line
fs-starter-build-rifleman-desc = Hold a lane with any gun. More damage, more toughness, faster reloads.
fs-starter-build-medic-name = Field Medic
fs-starter-build-medic-desc = Keep the squad standing. Stronger heals and a body that can take a hit.
fs-starter-build-fortifier-name = Fortifier
fs-starter-build-fortifier-desc = Barricades and deployables. Build the wall, then hold it.
fs-starter-build-banner = New here? Pick a starter build and you're ready to play.
fs-starter-build-button = STARTER BUILDS
fs-starter-build-window-title = Starter builds
fs-starter-build-window-hint = Applying a build spends your perk points on its six perks, level 1 across the board first, and slots them. Perks you already own are kept.
fs-starter-build-apply = APPLY
fs-starter-build-cost = Uses {$spent} of your {$points} PP
fs-starter-build-nothing = Nothing to buy: you already own this build at what your PP allows

fs-title-perk-master-name = Perk Master
fs-title-perk-master-desc = Took a perk to Gold mastery.
fs-title-mastery-offense-name = Offense Ascendant
fs-title-mastery-offense-desc = Gold mastery on every offense perk.
fs-title-mastery-defense-name = Defense Ascendant
fs-title-mastery-defense-desc = Gold mastery on every defense perk.
fs-title-mastery-utility-name = Utility Ascendant
fs-title-mastery-utility-desc = Gold mastery on every utility perk.
fs-title-mastery-economy-name = Economy Ascendant
fs-title-mastery-economy-desc = Gold mastery on every economy perk.
fs-title-mastery-hybrid-name = Hybrid Ascendant
fs-title-mastery-hybrid-desc = Gold mastery on every hybrid perk.

fs-mastery-none = No mastery
fs-mastery-bronze = Bronze
fs-mastery-silver = Silver
fs-mastery-gold = Gold
fs-mastery-tier-up = {$perk} mastery reached {$tier}!
fs-mastery-progress = Mastery: {$tier} · {$kills} / {$next} kills at level 4
fs-mastery-progress-max = Mastery: {$tier} · {$kills} kills
fs-mastery-hint = Kills while this perk is slotted at level 4 count toward its mastery.

fs-archetype-tooltip = Slot {$archetype} perks of one category for an archetype bonus, {$keystone} for its keystone.
fs-archetype-progress = {$category} {$count}/{$needed} for an archetype
fs-archetype-line = {$name}: {$effect} · keystone at {$needed} ({$count}/{$needed})
fs-archetype-keystone-line = {$name} KEYSTONE: {$effect}
fs-archetype-slayer = Slayer
fs-archetype-slayer-effect = +{ $damage }% damage to the horde
fs-archetype-bulwark = Bulwark
fs-archetype-bulwark-effect = { $resist }% less damage taken
fs-archetype-operator = Operator
fs-archetype-operator-effect = +{ $speed }% move speed
fs-archetype-tycoon = Tycoon
fs-archetype-tycoon-effect = +{ $credits }% kill credits
fs-archetype-specialist = Specialist
fs-archetype-specialist-effect = +{ $damage }% damage and credits, +{ $speed }% speed, { $resist }% less damage taken

fs-medievalist-no-guns = Your oath forbids firearms.
fs-perk-locked = That perk is locked in until the round ends.
fs-medievalist-no-armour = Your oath forbids armour.
