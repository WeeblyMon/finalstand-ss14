using Content.Shared._FinalStand.Humanoid;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void InitializeBodySize()
    {
        HeightSlider.OnValueChanged += _ => SetBodySize(true);
        WidthSlider.OnValueChanged += _ => SetBodySize(false);
        HeightReset.OnPressed += _ => ResetBodySize(true);
        WidthReset.OnPressed += _ => ResetBodySize(false);
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
        HeightSlider.MinValue = size.MinHeight;
        HeightSlider.MaxValue = size.MaxHeight;
        WidthSlider.MinValue = size.MinWidth;
        WidthSlider.MaxValue = size.MaxWidth;
        HeightSlider.SetValueWithoutEvent(height);
        WidthSlider.SetValueWithoutEvent(width);

        if (!MathHelper.CloseTo(height, Profile.Height) || !MathHelper.CloseTo(width, Profile.Width))
            Profile = Profile.WithSize(height, width);

        UpdateBodySizeLabels(size);
    }

    private void ResetBodySize(bool height)
    {
        if (CurrentBodySize() is not { } size)
            return;

        if (height)
            HeightSlider.Value = size.DefaultHeight;
        else
            WidthSlider.Value = size.DefaultWidth;
    }

    private void SetBodySize(bool heightMoved)
    {
        if (Profile == null || CurrentBodySize() is not { } size)
            return;

        var height = size.ClampHeight(HeightSlider.Value);
        var width = size.ClampWidth(WidthSlider.Value);

        if (heightMoved)
            width = size.ClampWidth(Math.Clamp(width, height / size.SizeRatio, height * size.SizeRatio));
        else
            height = size.ClampHeight(Math.Clamp(height, width / size.SizeRatio, width * size.SizeRatio));

        HeightSlider.SetValueWithoutEvent(height);
        WidthSlider.SetValueWithoutEvent(width);
        Profile = Profile.WithSize(height, width);
        UpdateBodySizeLabels(size);
        SetDirty();
        ReloadPreview();
    }

    private void UpdateBodySizeLabels(FSBodySize size)
    {
        if (Profile == null)
            return;

        HeightLabel.Text = Loc.GetString("fs-profile-editor-height", ("height", (int) MathF.Round(size.AverageHeight * HeightSlider.Value)));
        WidthLabel.Text = Loc.GetString("fs-profile-editor-width", ("width", (int) MathF.Round(size.AverageWidth * WidthSlider.Value)));

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

        var average = (HeightSlider.Value + WidthSlider.Value) / 2f;
        WeightLabel.Text = Loc.GetString("fs-profile-editor-weight", ("weight", (int) MathF.Round(weight * average)));
    }
}
