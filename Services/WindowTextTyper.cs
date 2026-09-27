using System.Runtime.InteropServices;
using System.Text;

namespace Verbal.Services;

internal static class WindowTextTyper
{
    private const int SwRestore = 9;
    private const uint InputKeyboard = 1;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;

    public static void TypeText(nint targetWindow, string text)
    {
        if (targetWindow == 0 || !IsWindow(targetWindow))
        {
            throw new InvalidOperationException("The target window is no longer available.");
        }

        if (IsIconic(targetWindow))
        {
            ShowWindow(targetWindow, SwRestore);
        }

        SetForegroundWindow(targetWindow);
        Thread.Sleep(150);
        if (GetForegroundWindow() != targetWindow)
        {
            throw new InvalidOperationException(
                "Windows would not activate the target window. Select it and try Insert again.");
        }

        var utf16 = Encoding.Unicode.GetBytes(text);
        var inputs = new Input[utf16.Length / sizeof(char) * 2];
        var index = 0;
        for (var offset = 0; offset < utf16.Length; offset += sizeof(char))
        {
            var codeUnit = BitConverter.ToUInt16(utf16, offset);
            inputs[index++] = Input.Unicode(codeUnit, KeyEventUnicode);
            inputs[index++] = Input.Unicode(codeUnit, KeyEventUnicode | KeyEventKeyUp);
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"Windows typed only {sent} of {inputs.Length / 2} transcript characters.");
        }
    }

    private static Input CreateInput(KeyboardInput keyboardInput) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion { Keyboard = keyboardInput }
        };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;

        public static Input Unicode(ushort character, uint flags) =>
            CreateInput(new KeyboardInput
            {
                VirtualKey = 0,
                ScanCode = character,
                Flags = flags,
                Time = 0,
                ExtraInfo = 0
            });
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint handle);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint handle);
}
