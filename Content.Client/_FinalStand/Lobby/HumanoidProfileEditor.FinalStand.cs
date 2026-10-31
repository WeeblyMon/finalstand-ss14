using Content.Shared._FinalStand.Humanoid;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void InitializeBodySize()
    {
        BodyHeightSlider.OnValueChanged += _ => SetBodySize(true);
        BodyWidthSlider.OnValueChanged += _ => SetBodySize(false);
        BodyHeightReset.OnPressed += _ => ResetBodySize(true);
        BodyWidthReset.OnPressed += _ => ResetBodySize(false);
    }

    private FSBodySize? CurrentBodySize()
    {
        return Profile != null && _prototypeManager.TryIndex(Profile.Species, out var species) ? species.BodySize : null;
    }

    private void UpdateBodySize()
    {
        if (Profile == null || CurrentBodySize() is not { } size)
            return;

        var height = size.ClampHeight(Profile.Height);
        var width = size.ClampWidth(Profile.Width);
        BodyHeightSlider.MinValue = size.MinHeight;
        BodyHeightSlider.MaxValue = size.MaxHeight;
        BodyWidthSlider.MinValue = size.MinWidth;
        BodyWidthSlider.MaxValue = size.MaxWidth;
        BodyHeightSlider.SetValueWithoutEvent(height);
        BodyWidthSlider.SetValueWithoutEvent(width);

        if (!MathHelper.CloseTo(height, Profile.Height) || !MathHelper.CloseTo(width, Profile.Width))
            Profile = Profile.WithSize(height, width);

        UpdateBodySizeLabels(size);
    }

    private void ResetBodySize(bool height)
    {
        if (CurrentBodySize() is not { } size)
            return;

        if (height)
            BodyHeightSlider.Value = size.DefaultHeight;
        else
            BodyWidthSlider.Value = size.DefaultWidth;
    }

    private void SetBodySize(bool heightMoved)
    {
        if (Profile == null || CurrentBodySize() is not { } size)
            return;

        var height = size.ClampHeight(BodyHeightSlider.Value);
        var width = size.ClampWidth(BodyWidthSlider.Value);

        if (heightMoved)
            width = size.ClampWidth(Math.Clamp(width, height / size.SizeRatio, height * size.SizeRatio));
        else
            height = size.ClampHeight(Math.Clamp(height, width / size.SizeRatio, width * size.SizeRatio));

        BodyHeightSlider.SetValueWithoutEvent(height);
        BodyWidthSlider.SetValueWithoutEvent(width);
        Profile = Profile.WithSize(height, width);
        UpdateBodySizeLabels(size);
        SetDirty();
        ReloadPreview();
    }

    private void UpdateBodySizeLabels(FSBodySize size)
    {
        if (Profile == null)
            return;

        BodyHeightLabel.Text = Loc.GetString("fs-profile-editor-height", ("height", (int) MathF.Round(size.AverageHeight * BodyHeightSlider.Value)));
        BodyWidthLabel.Text = Loc.GetString("fs-profile-editor-width", ("width", (int) MathF.Round(size.AverageWidth * BodyWidthSlider.Value)));

        var weight = 0f;
        if (_prototypeManager.TryIndex(Profile.Species, out var species)
            && _prototypeManager.Index(species.Prototype).TryGetComponent<FixturesComponent>(out var fixtures, _entManager.ComponentFactory))
        {
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Shape is PhysShapeCircle circle)
                    weight += MathF.PI * circle.Radius * circle.Radius * fixture.Density;
            }
        }

        var average = (BodyHeightSlider.Value + BodyWidthSlider.Value) / 2f;
        BodyWeightLabel.Text = Loc.GetString("fs-profile-editor-weight", ("weight", (int) MathF.Round(weight * average)));
    }
}
