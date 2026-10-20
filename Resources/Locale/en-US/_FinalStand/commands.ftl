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
    During prep, the coming wave becomes a Dark Wave and its warning starts now.
    During combat, the wave after this one is armed instead.
cmd-forcedarkwave-now = Dark Wave armed for this prep. Use forcenextwave to start it early.
cmd-forcedarkwave-next = Dark Wave armed for the next prep.

cmd-fsdebugzombies-desc = Dump every wave zombie's AI state.
cmd-fsdebugzombies-help = Usage: fsdebugzombies
    Prints position, target, speed, steering status, flow-field reachability and HTN
    blackboard keys per zombie, with totals for no-path, unreachable and stationary.
    Use it to diagnose idling, stranded or stuck zombies.

cmd-forcenextwave-desc = End the current wave and start the next one immediately.
cmd-forcenextwave-help = Usage: forcenextwave [wave]
    With no argument, advances to the next wave. Pass a number to jump to that wave.
    Note this skips WaveEndedEvent, so per-wave payouts and resets do not fire.
cmd-forcenextwave-done = Jumped to wave { $wave }.

cmd-pausewavespawns-desc = Toggle wave enemy spawning on or off.
cmd-pausewavespawns-help = Usage: pausewavespawns
    Toggles spawning. Enemies already alive are unaffected; the wave timer keeps running.
cmd-pausewavespawns-paused = Wave spawning paused. Enemies already alive stay.
cmd-pausewavespawns-resumed = Wave spawning resumed.

cmd-wave-rule-not-active = No wave rule is running.

cmd-startfinalstand-desc = Start a Final Stand round on FinalStandMap1.
cmd-startfinalstand-help = Usage: startfinalstand
    Loads the FinalStand preset and map, then starts the round.
cmd-startfinalstand-no-preset = { $preset } preset not found. Is the prototype loaded?
cmd-startfinalstand-no-map = { $map } not found. Is the prototype loaded?
cmd-startfinalstand-starting = Final Stand round starting on { $map }.

cmd-fsnetlag-desc = Shows how far behind the server your view of other entities is.
cmd-fsnetlag-help = Usage: fsnetlag
cmd-fsnetlag-result = View lag {$ticks} ticks ({$ms} ms), ping {$ping} ms, shot prediction {$prediction}.
cmd-fsnetlag-shot = Your last predicted bullet appeared {$ms} ms before the server's copy reached you.
cmd-fsnetlag-on = on
cmd-fsnetlag-off = off
