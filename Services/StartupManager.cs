using System.IO;
using Microsoft.Win32;

namespace Verbal.Services;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Verbal";
    private const string MinimizedArgument = "--minimized";

    public static bool IsEnabledForCurrentExecutable()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var registeredCommand = runKey?.GetValue(ValueName) as string;
        return string.Equals(
            registeredCommand,
            GetStartupCommand(),
            StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var runKey = enabled
            ? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            : Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (runKey is null)
        {
            if (enabled)
            {
                throw new InvalidOperationException(
                    "Could not open the current user's Windows startup settings.");
            }

            return;
        }

        if (enabled)
        {
            runKey.SetValue(ValueName, GetStartupCommand(), RegistryValueKind.String);
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string GetStartupCommand()
    {
        var appHostPath = Path.Combine(AppContext.BaseDirectory, "Verbal.exe");
        var executablePath = File.Exists(appHostPath)
            ? appHostPath
            : Environment.ProcessPath
                ?? throw new InvalidOperationException("Could not determine the Verbal executable path.");
        return $"\"{executablePath}\" {MinimizedArgument}";
    }
}
