using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Verbal.Services;

internal sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x5756;
    private const uint ModNoRepeat = 0x4000;
    private const int WhKeyboardLl = 13;
    private const int WmHotkey = 0x0312;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly HwndSource _source;
    private readonly HookProcedure _hookProcedure;
    private readonly KeyboardShortcut _pushToTalkShortcut;
    private nint _keyboardHook;
    private bool _pushToTalkIsDown;
    private bool _toggleRegistered;
    private bool _disposed;

    public event Action<bool>? PushToTalkChanged;
    public event Action? TogglePressed;
    public string? InitialWarning { get; private set; }

    public GlobalHotkeyService(
        HwndSource source,
        KeyboardShortcut pushToTalkShortcut,
        KeyboardShortcut toggleShortcut)
    {
        _source = source;
        _pushToTalkShortcut = pushToTalkShortcut;
        _source.AddHook(WindowProcedure);

        var toggleModifiers = ToWindowsModifiers(toggleShortcut.Modifiers) | ModNoRepeat;
        _toggleRegistered = RegisterHotKey(
            _source.Handle,
            HotkeyId,
            toggleModifiers,
            (uint)toggleShortcut.VirtualKey);
        if (!_toggleRegistered)
        {
            InitialWarning =
                $"{toggleShortcut} could not be registered (it may already be in use); use the toggle button.";
        }

        _hookProcedure = KeyboardHookCallback;
        _keyboardHook = SetWindowsHookEx(
            WhKeyboardLl,
            _hookProcedure,
            GetModuleHandle(Process.GetCurrentProcess().MainModule!.ModuleName),
            0);
        if (_keyboardHook == 0)
        {
            var hookWarning =
                $"Could not install the global {_pushToTalkShortcut} shortcut " +
                $"(Windows error {Marshal.GetLastWin32Error()}).";
            InitialWarning = InitialWarning is null
                ? hookWarning
                : $"{InitialWarning} {hookWarning}";
        }
    }

    private nint WindowProcedure(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            TogglePressed?.Invoke();
            handled = true;
        }

        return 0;
    }

    private nint KeyboardHookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var key = Marshal.ReadInt32(lParam);
            if (key == _pushToTalkShortcut.VirtualKey)
            {
                if (message is WmKeyDown or WmSysKeyDown &&
                    !_pushToTalkIsDown &&
                    MatchesModifiers(_pushToTalkShortcut.Modifiers))
                {
                    _pushToTalkIsDown = true;
                    PushToTalkChanged?.Invoke(true);
                }
                else if (message is WmKeyUp or WmSysKeyUp && _pushToTalkIsDown)
                {
                    _pushToTalkIsDown = false;
                    PushToTalkChanged?.Invoke(false);
                }
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_pushToTalkIsDown)
        {
            _pushToTalkIsDown = false;
            PushToTalkChanged?.Invoke(false);
        }

        _source.RemoveHook(WindowProcedure);
        if (_toggleRegistered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _toggleRegistered = false;
        }

        if (_keyboardHook != 0)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }
    }

    private delegate nint HookProcedure(int code, nint wParam, nint lParam);

    private static uint ToWindowsModifiers(ModifierKeys modifiers)
    {
        const uint modAlt = 0x0001;
        const uint modControl = 0x0002;
        const uint modShift = 0x0004;
        const uint modWindows = 0x0008;
        var result = 0u;
        if ((modifiers & ModifierKeys.Alt) != 0) result |= modAlt;
        if ((modifiers & ModifierKeys.Control) != 0) result |= modControl;
        if ((modifiers & ModifierKeys.Shift) != 0) result |= modShift;
        if ((modifiers & ModifierKeys.Windows) != 0) result |= modWindows;
        return result;
    }

    private static bool MatchesModifiers(ModifierKeys modifiers)
    {
        const int vkShift = 0x10;
        const int vkControl = 0x11;
        const int vkAlt = 0x12;
        const int vkLWin = 0x5B;
        const int vkRWin = 0x5C;
        return IsDown(vkShift) == ((modifiers & ModifierKeys.Shift) != 0)
            && IsDown(vkControl) == ((modifiers & ModifierKeys.Control) != 0)
            && IsDown(vkAlt) == ((modifiers & ModifierKeys.Alt) != 0)
            && (IsDown(vkLWin) || IsDown(vkRWin)) == ((modifiers & ModifierKeys.Windows) != 0);
    }

    private static bool IsDown(int virtualKey) => GetAsyncKeyState(virtualKey) < 0;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookType,
        HookProcedure callback,
        nint module,
        uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string moduleName);
}
