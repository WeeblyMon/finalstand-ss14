# SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

surgery-verb-text = Start surgery
surgery-verb-message = Begin surgery on this entity.
surgery-ui-window-title = Surgery
surgery-ui-window-require = Requires
surgery-ui-window-parts = < Parts
surgery-ui-window-surgeries = < Surgeries
surgery-ui-window-steps = < Steps
surgery-ui-window-steps-error-skills = You have no surgical skills.
surgery-ui-window-steps-error-table = You need an operating table for this.
surgery-ui-window-steps-error-armor = You need to remove their armor!
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

surgery-ui-guidance-unsterile = NEXT: {$step} — ready, but you are UNSTERILE (gloves + mask, or they get sepsis)

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
surgery-ui-guidance-armor = remove their armour first
surgery-ui-guidance-skills = you lack the surgical training
surgery-ui-guidance-previous = finish the earlier steps first
surgery-ui-guidance-unsterile-idle = No gloves and mask - operating like this will infect the patient.
surgery-ui-guidance-goal-first = GOAL: {$goal} — first you must finish {$requirement}
surgery-ui-guidance-not-needed = {$operation} is not needed for anything the patient currently has. Pick a treatment instead.
surgery-ui-guidance-toggle = Hints
