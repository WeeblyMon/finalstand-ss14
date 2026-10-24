fs-ladder-bracket-solo = Solo
fs-ladder-bracket-squad = 2-4 players
fs-ladder-bracket-crew = 5+ players
fs-ladder-bracket-any = Any size
fs-ladder-board-crew = Crew ({$count})
fs-ladder-new-record = [color=#FFD700][bold]NEW SERVER RECORD[/bold][/color] on {$difficulty}, {$bracket}!
fs-ladder-board-examine-empty = {$ladder}: no runs recorded yet. Be the first.
fs-ladder-board-examine = Record to beat ({$ladder}): {$players}, wave {$waves}.
fs-ladder-board-ladder = {$difficulty}, {$bracket}
fs-ladder-board-title = SERVER RECORDS
fs-ladder-board-weekly-title = WEEKLY CHALLENGE
fs-ladder-board-empty = No runs yet. Be the first!
fs-ladder-board-row-victory = WON W{$waves}
fs-ladder-board-row-wave = WAVE {$waves}
fs-ladder-window-title = Server records
fs-ladder-window-empty = No runs recorded here yet.
fs-ladder-window-loading = Loading records…
fs-ladder-col-rank = #
fs-ladder-col-result = Result
fs-ladder-col-time = Time
fs-ladder-col-kills = Kills
fs-ladder-col-players = Defenders
fs-ladder-col-date = Date
fs-ladder-result-victory = Victory
fs-ladder-result-wave = Wave {$waves}
fs-ladder-open-records = SERVER RECORDS

cmd-ladder-desc = Shows the best recorded runs for a difficulty and team size.
cmd-ladder-help = Usage: ladder [normal|hard|nightmare] [solo|squad|crew]
cmd-ladder-header = Best runs: {$difficulty}, {$bracket}
cmd-ladder-empty = No runs recorded yet.
cmd-ladder-row = {"  "}{$rank}. wave {$waves} in {$time}: {$players}
cmd-ladder-row-victory = {"  "}{$rank}. VICTORY in {$time}: {$players}

fs-discord-field-result = Result
fs-discord-field-difficulty = Difficulty
fs-discord-field-time = Time
fs-discord-field-kills = Kills
fs-discord-field-defenders = Defenders
fs-discord-field-mvp = MVP
fs-discord-field-medic = Lifesaver
fs-discord-field-record = New server record
fs-discord-field-rare-titles = Rare titles unlocked
fs-discord-field-nearly = So close
fs-discord-result-victory = Victory at wave 30
fs-discord-result-fell = Fell at wave {$waves}
fs-discord-mvp-kills = {$name}: {$kills} kills
fs-discord-mvp-revives = {$name}: {$revives} revives
fs-discord-rare-title = {$player} earned {$title}
fs-discord-nearly-beaten = {$short} {$short ->
    [one] wave
   *[other] waves
} short of the record held by {$holder} (wave {$waves})
fs-discord-record-first = First run on {$ladder}: {$players}
fs-discord-weekly-ladder = this week's challenge
fs-discord-record-beaten = {$players} took the {$ladder} record from {$previous}
fs-discord-title-victory = The station held!
fs-discord-title-fell = The station fell at wave {$waves}
fs-discord-footer = {$server} · round {$round}
fs-discord-ladder-name = {$difficulty} · {$bracket}
fs-discord-live-title = {$players} defenders are online now
fs-discord-live-wave = Fighting wave {$wave}. Jump in!
fs-discord-live-lobby = A round is about to start. Jump in!

fs-ladder-new-weekly-record = [color=#FFB300][bold]NEW WEEKLY CHALLENGE RECORD[/bold][/color]! The defenders earn the Weekly Champion title.
fs-ladder-assisted = An admin skipped waves this round, so it does not count for server records or the weekly challenge.
fs-ladder-weekly = This week
fs-ladder-weekly-header = Weekly challenge: {$name}

fs-weekly-none = There's no weekly challenge set up.
fs-weekly-too-late = The weekly challenge can only be started before wave 1, and only once per round.
fs-weekly-vote-running = A weekly challenge vote is already running.
fs-weekly-vote-used = The weekly challenge vote has already been held this round.
fs-weekly-vote-too-late = The weekly challenge vote passed, but wave 1 had already started. This round stays a normal round.
fs-weekly-vote-title = Play this week's challenge, {$name}?
fs-weekly-vote-yes = Yes, take the challenge
fs-weekly-vote-no = No, normal round
fs-weekly-started = This round is the weekly challenge: [bold]{$name}[/bold]. Clear wave {$wave} to earn the weekly reward.
fs-weekly-round-end-cleared = Weekly challenge {$name}: [color=#43A047]cleared[/color] (wave {$wave} reached).
fs-weekly-round-end-failed = Weekly challenge {$name}: not cleared. The target was wave {$wave}.
fs-weekly-reward = Weekly challenge cleared! +{$points} perk points. You can earn this once per week.

fs-weekly-last-armoury-name = Last Armoury
fs-weekly-last-armoury-desc = Hard difficulty, elite packs and half the prep time. Every special counts.
fs-weekly-restless-dead-name = Restless Dead
fs-weekly-restless-dead-desc = Big, slow zombies that heal. Bring sustained fire.
fs-weekly-tidal-wave-name = Tidal Wave
fs-weekly-tidal-wave-desc = Nightmare. Twice the zombies, faster and angrier. Hold to wave 30.

fs-title-weekly-champion-name = Weekly Champion
fs-title-weekly-champion-desc = Set the best run of a weekly challenge.

cmd-weekly-desc = Shows this week's challenge. "weekly vote" asks the round to play it.
cmd-weekly-help = Usage: weekly [vote]
cmd-weekly-summary = This week: {$name}. {$description} Difficulty {$difficulty}, modifiers: {$modifiers}. Clear wave {$wave} for +{$points} PP (once per week). Type "weekly vote" before wave 1 to play it.

fs-discord-weekly-open-title = New weekly challenge: {$name}
fs-discord-weekly-open-desc = {$description} Clear wave {$wave} for bonus perk points. Type "weekly vote" in game.
fs-discord-weekly-lastcall-title = Last call: {$name} ends tonight
fs-discord-weekly-lastcall-desc = The weekly challenge resets at midnight UTC.
fs-discord-weekly-leader = Current leader: {$players} (wave {$waves})
fs-discord-weekly-no-leader = Nobody has a run yet. The top spot is open.
