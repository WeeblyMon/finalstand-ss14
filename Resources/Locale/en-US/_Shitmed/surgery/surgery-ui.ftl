# SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

surgery-verb-text = Start surgery
surgery-verb-message = Begin surgery on this entity.
surgery-ui-window-title = Surgery
surgery-ui-guide-button = Guide
surgery-ui-guide-button-tooltip = Open the surgery guide: what each operation fixes and the order to do them in.
surgery-ui-window-title-part = Surgery - {$part}
surgery-ui-window-title-operation = Surgery - {$part}, {$operation}
surgery-ui-window-require = Requires
surgery-ui-window-parts = < Parts
surgery-ui-window-surgeries = < Surgeries
surgery-ui-window-steps = < Steps
surgery-ui-window-steps-error-skills = You have no surgical skills.
surgery-ui-window-steps-error-table = You need an operating table for this.
surgery-ui-window-steps-error-armor = Their clothing covers this part. Take it off first.
surgery-ui-window-steps-error-tools = Missing tools.
surgery-error-laying = They need to be laying down!
surgery-error-self-surgery = You can't perform surgery on yourself!
surgery-part-damage-evaded = {$user} narrowly evaded!

# FINALSTAND: single-screen layout.
surgery-ui-column-body = BODY
surgery-ui-column-operations = OPERATIONS
surgery-ui-column-procedure = PROCEDURE
surgery-ui-crumb-patient = Patient
surgery-ui-perform = Perform

# FINALSTAND: the persistent guidance bar.
surgery-ui-guidance-select = Select a body part, then an operation.
surgery-ui-guidance-start-with = START WITH: {$operation}
surgery-ui-guidance-nothing-to-do = Nothing left to do on this part.
surgery-ui-guidance-nothing-in-focus = No {$focus} work on this part.

surgery-ui-filter-all = All
surgery-ui-filter-bleeding = Bleeding
surgery-ui-filter-wounds = Wounds
surgery-ui-filter-bones = Bones
surgery-ui-filter-organs = Organs

surgery-ui-guidance-unsterile-note = (No gloves and mask: each step gives the patient a dose of toxin.)

surgery-ui-limb-condition = Condition: {$condition}
surgery-ui-limb-integrity = Integrity: {$current} / {$max}
surgery-ui-limb-bleeding = Bleeding ({$rate})
surgery-ui-limb-bone = Broken bone
surgery-ui-limb-organ = Organ damage
surgery-ui-limb-dismembered = Dismembered

surgery-ui-severity-healthy = Healthy
surgery-ui-severity-minor = Minor
surgery-ui-severity-moderate = Moderate
surgery-ui-severity-severe = Severe
surgery-ui-severity-critical = Critical
surgery-ui-severity-mangled = Mangled
surgery-ui-severity-severed = Severed
surgery-ui-guidance-cannot-operate = You cannot operate right now.
surgery-ui-guidance-complete = Operation complete.
surgery-ui-guidance-prerequisite = Finish the required operation above first.
surgery-ui-guidance-ready = NEXT: {$step} — ready
surgery-ui-guidance-blocked = NEXT: {$step} — {$reason}
surgery-ui-guidance-blocked-generic = you can't do this yet
surgery-ui-guidance-need-tool = needs {$tool} — none within reach
surgery-ui-guidance-need-table = move them onto an operating table
surgery-ui-guidance-armor = their clothing is in the way, take it off first
surgery-ui-guidance-skills = you lack the surgical training
surgery-ui-guidance-previous = finish the earlier steps first
surgery-ui-guidance-goal-first = GOAL: {$goal} — first you must finish {$requirement}
surgery-ui-guidance-toggle = Hints
surgery-ui-guidance-close-up = Repair done. Close up next: {$step}
surgery-ui-guidance-next-goal = Repair done. Next problem on this part: {$step}
surgery-ui-guidance-working = Working... wait for this step to finish.
surgery-ui-show-unavailable = Show {$count} unavailable
surgery-ui-hide-unavailable = Hide unavailable
surgery-ui-unavailable-tooltip = Not possible on this part right now. Either it isn't needed, or something else has to be fixed first.
surgery-ui-limb-missing = Missing. Click to reattach it from the {$part}.
