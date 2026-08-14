namespace Content.Goobstation.Common.SecondSkin;

[ByRefEvent]
public record struct GetSecondSkinDeductionEvent(string Coverage, int TraumaType, float Deduction = 0f);

[ByRefEvent]
public record struct ModifyDisgustEvent(float Delta);
