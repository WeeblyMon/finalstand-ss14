cmd-fsdbg-desc = Final Stand test helpers for checking the retention systems by hand. Use fsgiveap and fsgive for perk points and credits.
cmd-fsdbg-help = Usage: fsdbg <subcommand> [args] [player]
    state [player]                    everything the retention systems know about a player
    hours <h> [player]                set total playtime and refresh rookie status now (10+ h = veteran, 25+ h for mentors)
    fresh [player]                    wipe saved progress, 0 h, level 1, 0 PP: a brand-new player again
    waves <n> [player]                count the player as present for n waves this round
    level <n> [player]                set level (mentors need 15+)
    xp <amount> [player]              give raw XP through the normal path (multipliers, level-up PP)
    title <id> [player]               grant a title
    mastery <perk> <kills> [player]   add mastery kills (500 bronze, 2500 silver, 10000 gold)
    mentor <points> [player]          add mentor points to total and balance
    firstround [player]               try to pay the rookie first-round reward now
    tip <tip|all> [player]            show a tutorial card without marking it seen
    tipsreset [player]                forget every seen tip
    down [player]                     put the player into critical
    starter <build id> [player]       apply a starter build with the PP the player has
    body [player]                     spawn a human where the player is and move them into it
    open <perks|ladder> [player]      open the perk shop or server records window
    objective <id|random|complete|fail|progress n>   drive the bonus objective
    weekly [yyyy-MM-dd [HH:mm]]       which week and challenge a UTC date falls in
    seed <count> [difficulty] [players] [weekly]     add fake ladder runs
    clearladder                       delete every ladder run
    special [proto|all]               spawn special zombies near you as wave enemies
    discord                           print the last Discord payload as JSON
