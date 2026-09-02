using Content.Shared._FinalStand.CryoSleep;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._FinalStand.CryoSleep;

public sealed class FSCryoWakeupWindow : DefaultWindow
{
    private readonly RichTextLabel _label;
    private readonly Button _acceptButton;
    private readonly Button _denyButton;

    public event Action? OnAccepted;

    public FSCryoWakeupWindow()
    {
        Title = Loc.GetString("fs-cryo-wakeup-window-title");

        _label = new RichTextLabel();
        _label.SetMessage(Loc.GetString("fs-cryo-wakeup-window-prompt"));

        _acceptButton = new Button { Text = Loc.GetString("fs-cryo-wakeup-window-accept") };
        _denyButton = new Button { Text = Loc.GetString("fs-cryo-wakeup-window-deny") };

        _acceptButton.OnPressed += _ =>
        {
            SetBusy(true);
            OnAccepted?.Invoke();
        };

        _denyButton.OnPressed += _ => Close();

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                _label,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _acceptButton, _denyButton },
                },
            },
        });
    }

    public void ResetPrompt()
    {
        _label.SetMessage(Loc.GetString("fs-cryo-wakeup-window-prompt"));
        SetBusy(false);
    }

    public void HandleResponse(FSReturnToBodyStatus status)
    {
        if (status == FSReturnToBodyStatus.Success)
        {
            Close();
            return;
        }

        var key = status switch
        {
            FSReturnToBodyStatus.NoCryopodAvailable => "fs-cryo-wakeup-result-no-cryopod",
            FSReturnToBodyStatus.BodyMissing => "fs-cryo-wakeup-result-no-body",
            FSReturnToBodyStatus.NotAGhost => "fs-cryo-wakeup-result-not-a-ghost",
            _ => "fs-cryo-wakeup-result-disabled",
        };

        _label.SetMessage(Loc.GetString(key));
        SetBusy(false);
    }

    private void SetBusy(bool busy)
    {
        _acceptButton.Disabled = busy;
        _denyButton.Disabled = busy;
    }
}
