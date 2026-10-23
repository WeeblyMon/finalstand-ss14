namespace Content.Shared._FinalStand.Melee;

// Upgrades that change how a melee weapon's attacks work rather than its numbers.
public enum FSMeleeSignature : byte
{
    // Knife
    CoupDeGrace,
    Hemorrhage,
    Opportunist,

    // Bat
    GrandSlam,
    Ricochet,
    RunningStart,

    // Fire axe
    Unstoppable,
    Wildfire,
    Aftershock,

    // Halberd
    Brace,
    Cyclone,

    // Cutlass
    QuickReload,
    Broadside,

    // Chainsaw
    RipAndTear,
    Redline,
    Bloodthirsty,

    // Energy sword
    Tempest,
    LastBreath,
}

// The hit a weapon always crits on.
public enum FSMeleeCritWhen : byte
{
    Never,
    // The target is at least half dead.
    Wounded,
    // A body the weapon launched crashes into another.
    Crash,
    // A fully charged heavy.
    FullCharge,
    // The target is knocked down.
    Downed,
    // The rev is maxed.
    MaxRev,
    // The sweep chain is at its last stack.
    SweepChain,
}
