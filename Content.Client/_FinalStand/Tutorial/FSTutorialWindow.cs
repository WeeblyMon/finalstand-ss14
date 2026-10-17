using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client.Message;
using Content.Shared._FinalStand.Tutorial;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._FinalStand.Tutorial;

public sealed class FSTutorialWindow : DefaultWindow
{
    private readonly FSTutorialPrototype _tutorial;
    private readonly SpriteSystem _sprite;
    private readonly TextureRect _image;
    private readonly RichTextLabel _text;
    private readonly Label _dots;
    private readonly Button _back;
    private readonly Button _next;
    private int _page;

    public readonly CheckBox DontShowAgain;

    public FSTutorialWindow(FSTutorialPrototype tutorial, SpriteSystem sprite)
    {
        _tutorial = tutorial;
        _sprite = sprite;
        Title = Loc.GetString("fs-tutorial-window-title");
        MinSize = new Vector2(460, 420);
        Resizable = false;

        _image = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(128, 128),
            HorizontalAlignment = HAlignment.Center,
            TextureScale = new Vector2(4, 4),
        };
        _text = new RichTextLabel { MaxWidth = 420, VerticalExpand = true };
        _dots = new Label { HorizontalAlignment = HAlignment.Center, FontColorOverride = FSPalette.TextMuted };
        _back = new Button { Text = Loc.GetString("fs-tutorial-back") };
        _next = new Button { Text = Loc.GetString("fs-tutorial-next") };
        DontShowAgain = new CheckBox { Text = Loc.GetString("fs-tutorial-dont-show"), Pressed = true, HorizontalExpand = true };

        _back.OnPressed += _ => Show(_page - 1);
        _next.OnPressed += _ =>
        {
            if (_page + 1 < _tutorial.Pages.Count)
                Show(_page + 1);
            else
                Close();
        };

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(12),
            Children =
            {
                _image,
                _text,
                _dots,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 6,
                    Children = { DontShowAgain, _back, _next },
                },
            },
        });

        Show(0);
    }

    private void Show(int page)
    {
        _page = Math.Clamp(page, 0, _tutorial.Pages.Count - 1);
        var current = _tutorial.Pages[_page];

        _image.Texture = _sprite.Frame0(current.Image);
        _text.SetMarkup($"[font size=16][bold]{Loc.GetString(current.Title)}[/bold][/font]\n\n{Loc.GetString(current.Text)}");

        var dots = new string[_tutorial.Pages.Count];
        for (var i = 0; i < dots.Length; i++)
            dots[i] = i == _page ? "●" : "○";
        _dots.Text = string.Join(" ", dots);

        _back.Disabled = _page == 0;
        _next.Text = Loc.GetString(_page + 1 < _tutorial.Pages.Count ? "fs-tutorial-next" : "fs-tutorial-done");
    }
}
