fs-ailment-bleeding = BLEEDING
fs-ailment-broken = BROKEN BONE
fs-ailment-broken-examine = [color=#e8e2d0]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a broken bone.[/color]
fs-ailment-splinted = SPLINTED
fs-ailment-splinted-examine = [color=#e8e2d0]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a splinted bone.[/color]
fs-ailment-burned = BURNED
fs-ailment-burned-examine = [color=#ff8c2e]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } fresh burns.[/color]
fs-ailment-soaked = SOAKED
fs-ward-clotted = CLOTTED
fs-ailment-soaked-examine = [color=#a8d44a]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } dripping with bloater bile.[/color]
fs-slot-bone = BONE
fs-slot-burn = BURN
fs-slot-bloat = BLOAT
fs-need-gauze = GAUZE
fs-need-stapler = STAPLER
fs-need-ointment = OINTMENT
fs-need-defib = DEFIB
fs-ailment-bleeding-effect = {$rate} HP/s, no clotting while down
fs-ailment-bleeding-fix = gauze, stapler, beam
fs-ailment-broken-effect = -35% speed, sets itself in {$seconds} s
fs-ailment-broken-fix = stapler, beam (Bone-Knitter); splint eases
fs-ailment-splinted-effect = -15% speed, heals in {$seconds} s
fs-ailment-splinted-fieldops-effect = no slowdown under Field Ops, heals in {$seconds} s
fs-ailment-splinted-fix = stapler, tent
fs-ailment-burned-effect = no regen, heals halved, {$seconds} s left
fs-ailment-burned-fix = ointment
fs-ailment-soaked-effect = slowed, double damage taken, {$seconds} s left
fs-ward-clotted-effect = stops new bleeding, {$seconds} s left
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
fs-analyzer-regen-active = healing up to {$percent}% health
fs-analyzer-regen-recovering = recovering at the field hospital
fs-analyzer-regen-down = stopped while down
fs-analyzer-regen-blocked = blocked by {$ailment}
fs-analyzer-regen-waiting = starts after {$seconds} s without damage, up to {$percent}% health
fs-analyzer-chemicals = [bold]Chemicals[/bold]  {$list}
fs-analyzer-reagent-safe = {$name} {$amount}u (safe)
fs-analyzer-reagent-unsafe = [color=#c08a3e]{$name} {$amount}u (overdose)[/color]
fs-signature-runner = [color=#b3402f]Its claws open a wound with every hit.[/color]
fs-signature-giant = [color=#dfdcd7]Its slam, charge and boulders break bones.[/color]
fs-signature-bloater = [color=#a8b83a]When it bursts, its bile poisons everyone close by.[/color]
fs-signature-flamethrower = [color=#d9702e]Its fire leaves burns that stop you healing.[/color]
fs-signature-devastator = [color=#b3402f]Its chainsaw causes severe bleeding.[/color]
fs-signature-revenant = [color=#b3402f]Its blades cause heavy bleeding, and its grab breaks bones.[/color]
fs-signature-revenant-dark = [color=#b3402f]Its blades cause heavy bleeding.[/color]
fs-forecast-advice-bleeding = gauze
fs-forecast-advice-bone = splints
fs-forecast-advice-burn = ointment
fs-forecast-advice-bloat = water sprays, spread out
fs-forecast-line = {$new ->
    [true] NEW {$enemy}
   *[false] {$enemy}
} → {$ailment}
fs-forecast-header = [color=#d2a44e]Medical forecast, wave {$wave}:[/color] {$lines}. Bring {$advice}.
fs-hint-bleeding = [color=#b3402f]BLEEDING:[/color] you lose health every second and you cannot regenerate. Gauze stops it at once; a doctor's stapler closes heavy wounds. It clots by itself slowly, but not while you are down.
fs-hint-bone = [color=#dfdcd7]BROKEN BONE:[/color] -35% speed. Splint it, or it sets itself in 30 s. A doctor's stapler fixes it now.
fs-hint-burn = [color=#d9702e]BURNED:[/color] no regeneration, and bandages and the medigun heal you at half speed. Ointment clears it.
fs-hint-bloat = [color=#a8b83a]POISONED:[/color] bloater bile burns you for 12 seconds and stops you healing; for the first few seconds it also slows you and doubles the damage you take. A spray of water washes it off. Kill bloaters from range and spread out.
fs-slot-heart = HEART
fs-ailment-arrhythmia = ARRHYTHMIA
fs-ailment-arrhythmia-examine = [color=#4fb3d9]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } an uneven pulse.[/color]
fs-ailment-arrhythmia-effect = the heart skips every 6 s; dying 50% faster while down, {$seconds} s left
fs-ailment-arrhythmia-fix = cardiovert with the emergency defib, tent
fs-forecast-advice-heart = the emergency defib
fs-hint-heart = [color=#4fb3d9]ARRHYTHMIA:[/color] your heart skips a beat every 6 seconds, slowing you for a moment, and you die faster while down. A medic's emergency defib fixes it. Spread out from teslas.
fs-signature-tesla = [color=#4fb3d9]Its lightning knocks hearts out of rhythm.[/color]
fs-cardiovert-fine = Their heart rhythm is fine.
fs-cardiovert-recharging = The cardioversion charge is recharging.
fs-cardiovert-done = You shock {THE($target)}'s heart back into rhythm.
fs-ailment-poisoned = POISONED
fs-ailment-poisoned-examine = [color=#a8b83a]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } covered in reeking bile.[/color]
fs-ailment-poisoned-effect = 1 poison damage a second, no regen, {$seconds} s left
fs-ailment-poisoned-fix = a water spray, tent
fs-ailment-soaked-suffix = slowed
fs-ailment-with-suffix = {$name}, {$suffix}
fs-need-water = WATER
