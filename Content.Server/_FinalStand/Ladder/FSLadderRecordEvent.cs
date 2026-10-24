using Content.Shared._FinalStand.Ladder;

namespace Content.Server._FinalStand.Ladder;

[ByRefEvent]
public readonly record struct FSLadderRecordEvent(FSRunRecord Run, FSRunRecord? Previous);
