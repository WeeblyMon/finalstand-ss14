namespace Content.Client._FinalStand.WaveHud;

// HUD text rebuilt only when the value it shows changes; the overlay draws every frame.
public sealed class FSHudText<T> where T : IEquatable<T>
{
    private readonly Func<T, string> _format;
    private T _value = default!;
    private string? _text;

    public FSHudText(Func<T, string> format)
    {
        _format = format;
    }

    public string Get(T value)
    {
        if (_text == null || !_value.Equals(value))
        {
            _value = value;
            _text = _format(value);
        }

        return _text;
    }
}
