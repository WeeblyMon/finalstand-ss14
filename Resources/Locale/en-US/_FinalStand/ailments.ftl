fs-ailment-bleeding = BLEEDING
fs-ailment-broken = BROKEN BONE
fs-ailment-broken-examine = [color=#e8e2d0]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a broken bone.[/color]
fs-ailment-splinted = SPLINTED
fs-ailment-splinted-examine = [color=#e8e2d0]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a splinted bone.[/color]
fs-ailment-burned = BURNED
fs-ailment-burned-examine = [color=#ff8c2e]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } fresh burns.[/color]
fs-ailment-soaked = SOAKED
fs-ward-clotted = CLOTTED
fs-ward-braced = BRACED
fs-ward-warded = WARDED
fs-ailment-soaked-examine = [color=#a8d44a]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } dripping with bloater bile.[/color]
fs-slot-bone = BONE
fs-slot-burn = BURN
fs-slot-bloat = BLOAT
fs-need-gauze = GAUZE
fs-need-stapler = STAPLER
fs-need-ointment = OINTMENT
fs-need-defib = DEFIB
fs-ailment-bleeding-effect = {$rate} HP/s, no clotting while down
fs-ailment-bleeding-fix = gauze, stapler, beam, TXA, Coagulant
fs-ailment-broken-effect = -35% speed, sets itself in {$seconds} s
fs-ailment-broken-fix = stapler, beam (Bone-Knitter); splint eases
fs-ailment-splinted-effect = -15% speed, heals in {$seconds} s
fs-ailment-splinted-fieldops-effect = no slowdown under Field Ops, heals in {$seconds} s
fs-ailment-splinted-fix = stapler, tent
fs-ailment-burned-effect = no regen, heals halved, {$seconds} s left
fs-ailment-burned-fix = ointment, Kelotane, Dermaline
fs-ailment-soaked-effect = slowed, double damage taken, {$seconds} s left
fs-ward-clotted-effect = stops new bleeding, {$seconds} s left
fs-ward-braced-effect = stops broken bones, {$seconds} s left
fs-ward-warded-effect = stops burns, {$seconds} s left
fs-ailment-name-level = {$name} {$level}
fs-ailment-inflicted-by = {$ailment} ({$source})
fs-vitals-regen = REGEN
fs-vitals-recovering = RECOVERING
fs-analyzer-down-held = DOWN · held
fs-analyzer-down-clock = DOWN · dies in {$seconds} s
fs-analyzer-health = Health {$percent}%
fs-analyzer-ailments = [bold]Ailments[/bold]
fs-analyzer-ailment-line = [color={$color}]{$name}[/color]  {$effect}  {$fix}
fs-analyzer-fix = [color=#96938d]Fix: {$fix}[/color]
fs-analyzer-regen = [bold]Regen[/bold]  {$state}
fs-analyzer-regen-active = healing up to half health
fs-analyzer-regen-down = stopped while down
fs-analyzer-regen-blocked = blocked by {$ailment}
fs-analyzer-regen-waiting = starts after 6 s without damage
fs-analyzer-chemicals = [bold]Chemicals[/bold]  {$list}
fs-analyzer-reagent-safe = {$name} {$amount}u (safe)
fs-analyzer-reagent-unsafe = [color=#c08a3e]{$name} {$amount}u (overdose)[/color]
