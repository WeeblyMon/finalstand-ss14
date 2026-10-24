using Robust.Shared.Network;

namespace Content.Server._FinalStand.Onboarding;

[ByRefEvent]
public readonly record struct FSRookieFirstRoundEvent(NetUserId Rookie);
