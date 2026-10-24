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
surgery-ui-column-plan = PLAN
surgery-ui-crumb-patient = Patient
surgery-ui-perform = Perform
surgery-ui-do-all = Do all
surgery-ui-do-all-tooltip = Keep going: start each next step by itself while the tools are within reach. Moving away or a missing tool stops it.

surgery-ui-plan-header = {$part}: {$problems ->
    [0] nothing to fix
    [one] 1 problem
   *[other] {$problems} problems
}
surgery-ui-plan-header-open = {$part}: {$problems ->
    [0] open, nothing left to fix
    [one] open, 1 problem
   *[other] open, {$problems} problems
}
surgery-ui-plan-procedure = {$part}: {$operation}
surgery-ui-plan-back = Back to plan
surgery-ui-plan-open = Open up: {$operation}
surgery-ui-plan-close = Close up: {$operation}
surgery-ui-plan-fixed = {$operation}: fixed
surgery-ui-plan-waiting = Waiting: {$reason}
surgery-ui-plan-other = Other procedures ({$count})
surgery-ui-plan-other-hide = Hide other procedures
surgery-ui-plan-other-tooltip = Amputation, transplants and implants. None of these treat an injury.
surgery-ui-step-running = in progress...
surgery-ui-step-details = {$tool} ({$where}) · {$seconds}s
surgery-ui-step-details-needs = needs {$tool} · {$seconds}s
surgery-ui-step-details-time = {$seconds}s

# FINALSTAND: the persistent guidance bar.
surgery-ui-guidance-toggle = Hints
surgery-ui-guidance-select = Select a body part on the doll.
surgery-ui-guidance-cannot-operate = You cannot operate right now.
surgery-ui-guidance-next = NEXT: {$step} · {$details}
surgery-ui-guidance-blocked = NEXT: {$step} · {$reason}
surgery-ui-guidance-starting = Starting {$step}...
surgery-ui-guidance-working = Working... wait for this step to finish.
surgery-ui-guidance-waiting = {$problem} is waiting. {$reason}
surgery-ui-guidance-all-fixed = Everything on this part is fixed and closed.
surgery-ui-guidance-nothing-to-do = Nothing to fix on this part.
surgery-ui-guidance-unsterile-note = (No gloves and mask: each step gives the patient a dose of toxin.)

surgery-ui-limb-condition = Condition: {$condition}
surgery-ui-limb-integrity = Integrity: {$current} / {$max}
surgery-ui-limb-bleeding = Bleeding ({$rate})
surgery-ui-limb-bone = Broken bone
surgery-ui-limb-organ = Organ damage
surgery-ui-limb-dismembered = Dismembered
surgery-ui-limb-missing = Missing. Click to reattach it from the {$part}.

surgery-ui-severity-healthy = Healthy
surgery-ui-severity-minor = Minor
surgery-ui-severity-moderate = Moderate
surgery-ui-severity-severe = Severe
surgery-ui-severity-critical = Critical
surgery-ui-severity-mangled = Mangled
surgery-ui-severity-severed = Severed
