using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._FinalStand.CryoSleep;

// Small yes/no prompt for actions that cannot be undone.
public sealed class FSConfirmWindow : DefaultWindow
{
    public event Action? OnAccepted;

    public FSConfirmWindow(string title, string prompt, string accept, string deny)
    {
        Title = title;

        var label = new RichTextLabel();
        label.SetMessage(prompt);

        var acceptButton = new Button { Text = accept };
        var denyButton = new Button { Text = deny };

        acceptButton.OnPressed += _ =>
        {
            OnAccepted?.Invoke();
            Close();
        };

        denyButton.OnPressed += _ => Close();

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                label,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { acceptButton, denyButton },
                },
            },
        });
    }
}
