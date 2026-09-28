using System.Windows.Input;

namespace Verbal.Services;

internal readonly record struct KeyboardShortcut(Key Key, ModifierKeys Modifiers)
{
    public int VirtualKey => KeyInterop.VirtualKeyFromKey(Key);

    public bool IsSafe =>
        Key != Key.None &&
        Key is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System) &&
        VirtualKey != 0 &&
        (Modifiers != ModifierKeys.None || Key is >= Key.F1 and <= Key.F24);

    public static KeyboardShortcut ParseOrDefault(string? value, Key defaultKey)
    {
        if (TryParse(value, out var shortcut))
        {
            return shortcut;
        }

        return new KeyboardShortcut(defaultKey, ModifierKeys.None);
    }

    public static bool TryParse(string? value, out KeyboardShortcut shortcut)
    {
        shortcut = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !Enum.TryParse(parts[^1], true, out Key key))
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            var modifier = part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModifierKeys.Control,
                "alt" => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win" or "windows" => ModifierKeys.Windows,
                _ => (ModifierKeys?)null
            };
            if (modifier is null || (modifiers & modifier.Value) != 0)
            {
                return false;
            }

            modifiers |= modifier.Value;
        }

        shortcut = new KeyboardShortcut(key, modifiers);
        return shortcut.IsSafe;
    }

    public string Serialize()
    {
        var parts = new List<string>();
        if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(GetKeyDisplayName());
        return string.Join(" + ", parts);
    }

    private string GetKeyDisplayName()
    {
        var keyName = Key.ToString();
        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1]))
        {
            return keyName[1].ToString();
        }

        return keyName;
    }
}
