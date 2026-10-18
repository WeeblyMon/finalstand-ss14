fs-mentor-earned = +{$points} mentor {$points ->
    [one] point
   *[other] points
}: {$reason}
fs-mentor-reason-revive = you revived {$rookie}
fs-mentor-reason-heal = you healed {$rookie}
fs-mentor-reason-escort = {$rookie} survived the wave at your side
fs-mentor-reason-first-round = {$rookie} finished their first round with you
fs-mentor-shop-unknown = There's no mentor reward called "{$item}".
fs-mentor-shop-owned = You already have that reward.
fs-mentor-shop-short = That costs {$cost} mentor points; you have {$balance}.
fs-mentor-shop-bought = Bought for {$cost} mentor points.
fs-mentor-shop-offline = You need to be connected to buy perk points.

fs-title-mentor-name = Mentor
fs-title-mentor-desc = Helped new players find their feet.
fs-title-guardian-angel-name = Guardian Angel
fs-title-guardian-angel-desc = Kept a lot of rookies alive.
fs-title-shepherd-name = Shepherd
fs-title-shepherd-desc = Brought the new flock home safe.
fs-title-lighthouse-name = Lighthouse
fs-title-lighthouse-desc = Rookies run towards you when things go wrong.
fs-title-living-legend-name = Living Legend
fs-title-living-legend-desc = Half the server learned the game from you.

cmd-mentor-desc = Shows your mentor points and the mentor reward shop.
cmd-mentor-help = Usage: mentor [buy <reward>]
cmd-mentor-no-player = Only players can be mentors.
cmd-mentor-summary = Mentor points: {$balance} to spend, {$total} earned in total, {$week}/{$weekCap} this week. Eligible to earn: {$eligible} (25+ hours played and level 15+). Revive, heal and stick close to rookies to earn them.
cmd-mentor-shop-row = {"  "}mentor buy {$id}: {$title} title ({$cost} points)
cmd-mentor-shop-perk-row = {"  "}mentor buy {$id}: {$points} perk {$points ->
    [one] point
   *[other] points
} ({$cost} points)

fs-lobby-perks-button = Perks
fs-lobby-titles-button = Titles
fs-lobby-mentor-button = Mentor

fs-titles-window-title = Titles
fs-titles-heading = YOUR TITLES
fs-titles-summary = {$owned} of {$total} unlocked. Your equipped title shows on the scoreboard and when people examine you, and gives a small bonus.
fs-titles-equip = Equip
fs-titles-equipped = Equipped
fs-titles-unequip = Unequip
fs-titles-locked = Locked

fs-mentor-window-title = Mentoring
fs-mentor-window-loading = Loading...
fs-mentor-heading = MENTOR POINTS
fs-mentor-intro = Veterans earn mentor points by keeping new players alive and helping them through their first rounds. Spend them on perk points and titles below.
fs-mentor-stat-balance = To spend
fs-mentor-stat-total = Earned in total
fs-mentor-stat-week = This week
fs-mentor-week-value = {$points} / {$cap}
fs-mentor-eligible = You can earn mentor points.
fs-mentor-not-eligible = You can't earn mentor points yet.
fs-mentor-req-hours = {$hours} / {$needed} hours played
fs-mentor-req-level = Level {$level} / {$needed}
fs-mentor-earn-heading = How to earn them
fs-mentor-earn-points = +{$points}
fs-mentor-earn-revive = Revive a rookie
fs-mentor-earn-heal = Heal a rookie, per 50 health
fs-mentor-earn-escort = Survive a wave near a rookie
fs-mentor-earn-first-round = A rookie finishes their first round with you
fs-mentor-earn-rules = Points are capped per rookie, per round and per week. Hurting a rookie stops you earning from them for the rest of the round.
fs-mentor-shop-heading = Mentor shop
fs-mentor-shop-title = {$title} title
fs-mentor-shop-buy = Buy ({$cost})
fs-mentor-shop-owned-label = Owned
fs-mentor-shop-perk-points = {$points} perk {$points ->
    [one] point
   *[other] points
}
fs-mentor-shop-perk-desc = Spend them in the perk machine. You can buy these as often as you like.

fs-title-buff-fresh-meat = +5% XP
fs-title-buff-survivor = +5 max health
fs-title-buff-station-held = +3% credits from kills
fs-title-buff-iron-wall = 3% less damage taken
fs-title-buff-nightmare-breaker = +3% damage to bosses
fs-title-buff-beyond-the-wall = +3% damage in endless waves
fs-title-buff-weekly-champion = +5% bonus objective rewards
fs-title-buff-perk-master = +2% XP and +2% credits from kills
fs-title-buff-offense = +2% damage
fs-title-buff-defense = 2% less damage taken
fs-title-buff-utility = +2% move speed
fs-title-buff-economy = +4% credits from kills and bonus objectives
fs-title-buff-hybrid = +1% damage, 1% less damage taken, +1% move speed
fs-title-buff-mentor = +10% healing you do
fs-title-buff-guardian-angel = +15% healing on downed teammates, so your revives land faster
fs-title-buff-shepherd = +3% XP and +3% credits from kills
fs-title-buff-lighthouse = +2% move speed and +5% healing you do
fs-title-buff-living-legend = +2% damage, 2% less damage taken and +3 max health
fs-titles-buff-line = Equipped bonus: {$buff}
fs-badge-examine-title-buff = [color=#96938d]Title bonus: {$buff}[/color]
