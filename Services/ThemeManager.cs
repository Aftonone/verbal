using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace Verbal.Services;

internal static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Verbal",
        "settings.json");

    private static readonly string LegacySettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhisperVoice",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static UserSettings LoadSettings()
    {
        if (File.Exists(SettingsPath))
        {
            return DeserializeSettings(SettingsPath);
        }

        if (File.Exists(LegacySettingsPath))
        {
            var settings = DeserializeSettings(LegacySettingsPath);
            SaveSettings(settings);
            return settings;
        }

        return new UserSettings();
    }

    private static UserSettings DeserializeSettings(string path) =>
        JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The saved app settings are empty.");

    public static void SaveSettings(UserSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("Could not determine the settings directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static void Apply(Application application, bool darkMode)
    {
        var palette = darkMode
            ? new Dictionary<string, Color>
            {
                ["WindowBackground"] = Color.FromRgb(25, 28, 36),
                ["Ink"] = Color.FromRgb(235, 238, 246),
                ["MutedInk"] = Color.FromRgb(157, 166, 184),
                ["Accent"] = Color.FromRgb(132, 139, 255),
                ["AccentHover"] = Color.FromRgb(106, 114, 238),
                ["CardBackground"] = Color.FromRgb(35, 39, 49),
                ["Border"] = Color.FromRgb(57, 63, 77),
                ["ButtonHover"] = Color.FromRgb(46, 51, 63),
                ["ControlTrack"] = Color.FromRgb(54, 60, 73),
                ["SelectorBackground"] = Color.FromRgb(42, 47, 59),
                ["SelectorAffordance"] = Color.FromRgb(53, 59, 73),
                ["StatusIdle"] = Color.FromRgb(133, 143, 162),
                ["SelectedItemInk"] = Color.FromRgb(25, 28, 36)
            }
            : new Dictionary<string, Color>
            {
                ["WindowBackground"] = Color.FromRgb(245, 247, 251),
                ["Ink"] = Color.FromRgb(23, 32, 51),
                ["MutedInk"] = Color.FromRgb(104, 115, 138),
                ["Accent"] = Color.FromRgb(82, 92, 235),
                ["AccentHover"] = Color.FromRgb(66, 75, 208),
                ["CardBackground"] = Colors.White,
                ["Border"] = Color.FromRgb(231, 234, 241),
                ["ButtonHover"] = Color.FromRgb(245, 246, 250),
                ["ControlTrack"] = Color.FromRgb(233, 234, 241),
                ["SelectorBackground"] = Color.FromRgb(248, 249, 252),
                ["SelectorAffordance"] = Color.FromRgb(239, 242, 248),
                ["StatusIdle"] = Color.FromRgb(154, 163, 181),
                ["SelectedItemInk"] = Colors.White
            };

        foreach (var (key, color) in palette)
        {
            application.Resources[key] = new SolidColorBrush(color);
        }
    }

    internal sealed class UserSettings
    {
        public UserSettings()
        {
        }

        public bool DarkMode { get; set; }
        public string? ModelFile { get; set; }
        public string? InferenceBackend { get; set; }
        public string? PushToTalkShortcut { get; set; }
        public string? ToggleShortcut { get; set; }
        public int? MicrophoneDeviceNumber { get; set; }
        public string? MicrophoneName { get; set; }
    }
}
