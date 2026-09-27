using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Verbal.Services;

internal sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x5756;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF8 = 0x77;
    private const uint VkF9 = 0x78;
    private const int WhKeyboardLl = 13;
    private const int WmHotkey = 0x0312;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly HwndSource _source;
    private readonly HookProcedure _hookProcedure;
    private nint _keyboardHook;
    private bool _f8IsDown;
    private bool _toggleRegistered;
    private bool _disposed;

    public event Action<bool>? PushToTalkChanged;
    public event Action? TogglePressed;
    public string? InitialWarning { get; private set; }

    public GlobalHotkeyService(HwndSource source)
    {
        _source = source;
        _source.AddHook(WindowProcedure);

        _toggleRegistered = RegisterHotKey(_source.Handle, HotkeyId, ModNoRepeat, VkF9);
        if (!_toggleRegistered)
        {
            InitialWarning = "F9 is already registered by another app; use the toggle button.";
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
                $"Could not install the global F8 shortcut (Windows error {Marshal.GetLastWin32Error()}).";
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
            if (key == VkF8)
            {
                if (message is WmKeyDown or WmSysKeyDown && !_f8IsDown)
                {
                    _f8IsDown = true;
                    PushToTalkChanged?.Invoke(true);
                }
                else if (message is WmKeyUp or WmSysKeyUp && _f8IsDown)
                {
                    _f8IsDown = false;
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
        if (_f8IsDown)
        {
            _f8IsDown = false;
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
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string moduleName);
}
