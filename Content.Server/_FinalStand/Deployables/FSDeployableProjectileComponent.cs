namespace Content.Server._FinalStand.Deployables;

// Marks shots fired by a deployable, which pay out without the deployer's held-weapon bonus.
[RegisterComponent]
[Access(typeof(Content.Server._FinalStand.Economy.FSMoneyOnHitSystem))]
public sealed partial class FSDeployableProjectileComponent : Component;
