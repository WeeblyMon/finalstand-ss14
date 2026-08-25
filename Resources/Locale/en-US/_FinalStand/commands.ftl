cmd-fsrevenant-desc = Revenant debugging: spawn, lock to one ability, inspect cooldowns.
cmd-fsrevenant-help = Usage: fsrevenant <spawn [distance] | lock <name> | unlock | info | clear | stats | resetstats>
    spawn [distance]  Spawn a Revenant N tiles east of you (default 5).
    lock <name>       Restrict every Revenant to one ability and nothing else, repeating on the
                      global cooldown so you can watch it in isolation.
                      Names: Execute, Grab, Bind, Slice, Bolt.
                      Range, line-of-sight and health gates still apply, so an ability can still
                      refuse; the server console logs each fired/refused transition with the range.
    unlock            Return every Revenant to its normal ability priority.
    info              Print each Revenant's channel state, lock, cooldowns and marked target.
    clear             Delete all Revenants.
    stats             Print combo/execute/mark counters since round start or last reset.
    resetstats        Zero the counters.

cmd-forcedarkwave-desc = Arm the next wave to be a Dark Wave.
cmd-forcedarkwave-help = Usage: forcedarkwave
    Flags the upcoming wave as a Dark Wave. This takes effect at the next prep phase,
    so follow it with forcenextwave to start that wave immediately.

cmd-fsdebugzombies-desc = Dump every wave zombie's AI state.
cmd-fsdebugzombies-help = Usage: fsdebugzombies
    Prints position, target, speed, steering status, flow-field reachability and HTN
    blackboard keys per zombie, with totals for no-path, unreachable and stationary.
    Use it to diagnose idling, stranded or stuck zombies.

cmd-forcenextwave-desc = End the current wave and start the next one immediately.
cmd-forcenextwave-help = Usage: forcenextwave [wave]
    With no argument, advances to the next wave. Pass a number to jump to that wave.
    Note this skips WaveEndedEvent, so per-wave payouts and resets do not fire.

cmd-pausewavespawns-desc = Toggle wave enemy spawning on or off.
cmd-pausewavespawns-help = Usage: pausewavespawns
    Toggles spawning. Enemies already alive are unaffected; the wave timer keeps running.

cmd-startfinalstand-desc = Start a Final Stand round on FinalStandMap1.
cmd-startfinalstand-help = Usage: startfinalstand
    Loads the FinalStand preset and map, then starts the round.
