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

fs-title-mentor-name = Mentor
fs-title-mentor-desc = Helped new players find their feet.
fs-title-guardian-angel-name = Guardian Angel
fs-title-guardian-angel-desc = Kept a lot of rookies alive.

cmd-mentor-desc = Shows your mentor points and the mentor reward shop.
cmd-mentor-help = Usage: mentor [buy <reward>]
cmd-mentor-no-player = Only players can be mentors.
cmd-mentor-summary = Mentor points: {$balance} to spend, {$total} earned in total, {$week}/{$weekCap} this week. Eligible to earn: {$eligible} (25+ hours played and level 15+). Revive, heal and stick close to rookies to earn them.
cmd-mentor-shop-row = {"  "}mentor buy {$id}: {$title} title ({$cost} points)
