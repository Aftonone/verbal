using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Verbal.Services;

namespace Verbal;

public partial class MainWindow : Window
{
    private readonly AudioRecorder _recorder = new();
    private WhisperTranscriber? _transcriber;
    private WhisperInstallation? _installation;
    private WhisperExecutable? _recordingExecutable;
    private ThemeManager.UserSettings _settings = new();
    private readonly DispatcherTimer _targetWindowTimer = new();
    private GlobalHotkeyService? _hotkeys;
    private nint _lastExternalWindow;
    private nint _recordingTargetWindow;
    private string? _recordingModelPath;
    private RecordingMode _recordingMode;
    private bool _isProcessing;
    private bool _isClosing;
    private bool _loadingSettings = true;

    public MainWindow()
    {
        InitializeComponent();
        Icon = new BitmapImage(new Uri(
            Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"),
            UriKind.Absolute));
        try
        {
            _settings = ThemeManager.LoadSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            MessageBox.Show(
                this,
                $"Saved appearance settings could not be loaded. The app will use light mode.\n\n{ex.Message}",
                "Settings could not be loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        ThemeManager.Apply(Application.Current, _settings.DarkMode);
        DarkModeToggle.IsChecked = _settings.DarkMode;
        var microphones = AudioRecorder.GetDevices();
        MicrophoneSelector.ItemsSource = microphones;
        MicrophoneSelector.SelectedItem = microphones.FirstOrDefault(device =>
            string.Equals(device.Name, _settings.MicrophoneName, StringComparison.Ordinal))
            ?? microphones.FirstOrDefault(device =>
                device.DeviceNumber == (_settings.MicrophoneDeviceNumber ?? -1))
            ?? microphones[0];
        _recorder.InputLevelChanged += level =>
            Dispatcher.BeginInvoke(() => UpdateInputLevel(level));

        try
        {
            _installation = WhisperInstallation.Find();
            var executables = _installation.GetExecutables();
            InferenceSettingsPanel.Visibility = executables.Any(executable =>
                executable.Backend == WhisperBackend.Cuda)
                ? Visibility.Visible
                : Visibility.Collapsed;
            InferenceSelector.ItemsSource = executables;
            InferenceSelector.SelectedItem = executables.FirstOrDefault(executable =>
                string.Equals(
                    executable.Backend.ToString(),
                    _settings.InferenceBackend,
                    StringComparison.OrdinalIgnoreCase))
                ?? executables.FirstOrDefault(executable => executable.Backend == WhisperBackend.Cpu)
                ?? executables.FirstOrDefault();
            _recordingExecutable = InferenceSelector.SelectedItem as WhisperExecutable;
            _transcriber = new WhisperTranscriber();
            var models = _installation.GetModels();
            ModelSelector.ItemsSource = models;
            ModelSelector.SelectedItem = models.FirstOrDefault(model =>
                string.Equals(model.FileName, _settings.ModelFile, StringComparison.OrdinalIgnoreCase))
                ?? models.FirstOrDefault(model =>
                    string.Equals(model.FileName, "ggml-base.en.bin", StringComparison.OrdinalIgnoreCase))
                ?? models.FirstOrDefault();
            if (ModelSelector.SelectedItem is WhisperModel selectedModel)
            {
                _recordingModelPath = selectedModel.Path;
            }
            else
            {
                ModelHintText.Text =
                    "No models installed. Add ggml model .bin files to whisper_cpp\\models.";
                StatusText.Text = "No Whisper models installed";
                StatusDetailText.Text = "Add a model file to the models folder, then restart the app.";
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(218, 142, 50));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Whisper files not found",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
            return;
        }
        finally
        {
            _loadingSettings = false;
        }

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        _targetWindowTimer.Interval = TimeSpan.FromMilliseconds(250);
        _targetWindowTimer.Tick += TrackTargetWindow;
        _targetWindowTimer.Start();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        if (source is null)
        {
            UpdateStatus(
                "Shortcuts unavailable",
                "Use the on-screen controls; Windows did not create a shortcut window.",
                StatusKind.Error);
            return;
        }

        _hotkeys = new GlobalHotkeyService(source);
        if (_hotkeys.InitialWarning is not null)
        {
            UpdateStatus("Shortcut unavailable", _hotkeys.InitialWarning, StatusKind.Error);
        }
        _hotkeys.PushToTalkChanged += isDown =>
            Dispatcher.BeginInvoke(() =>
            {
                if (isDown)
                {
                    StartRecording(RecordingMode.Hold);
                }
                else if (_recordingMode == RecordingMode.Hold)
                {
                    StopRecording();
                }
            });
        _hotkeys.TogglePressed += () =>
            Dispatcher.BeginInvoke(ToggleRecording);
    }

    private void TrackTargetWindow(object? sender, EventArgs e)
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0)
        {
            return;
        }

        GetWindowThreadProcessId(foreground, out var processId);
        if (processId != Environment.ProcessId)
        {
            _lastExternalWindow = foreground;
        }
    }

    private void UpdateInputLevel(float level)
    {
        var normalized = Math.Clamp(level / 100f, 0, 1);
        InnerSoundWave.Opacity = 0.24 + normalized * 0.65;
        MiddleSoundWave.Opacity = Math.Clamp((normalized - 0.04) / 0.48 * 0.82, 0, 0.82);
        OuterSoundWave.Opacity = Math.Clamp((normalized - 0.18) / 0.52 * 0.74, 0, 0.74);
    }

    private void ResetInputLevel()
    {
        InnerSoundWave.Opacity = 0;
        MiddleSoundWave.Opacity = 0;
        OuterSoundWave.Opacity = 0;
    }

    private void MicrophoneSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MicrophoneSelector.SelectedItem is MicrophoneDevice microphone &&
            _recordingMode == RecordingMode.None &&
            !_isProcessing)
        {
            _recorder.DeviceNumber = microphone.DeviceNumber;
            if (!_loadingSettings)
            {
                _settings.MicrophoneDeviceNumber = microphone.DeviceNumber;
                _settings.MicrophoneName = microphone.Name;
                SaveSettings("Microphone preference saved.");
            }
        }
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is WhisperModel model &&
            _recordingMode == RecordingMode.None &&
            !_isProcessing)
        {
            _recordingModelPath = model.Path;
            if (!_loadingSettings)
            {
                _settings.ModelFile = model.FileName;
                SaveSettings($"Using {model.DisplayName}.");
            }
        }
    }

    private void InferenceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InferenceSelector.SelectedItem is WhisperExecutable executable)
        {
            _recordingExecutable = executable;
            if (!_loadingSettings &&
                _recordingMode == RecordingMode.None &&
                !_isProcessing)
            {
                _settings.InferenceBackend = executable.Backend.ToString();
                SaveSettings($"Using {executable.DisplayName} inference.");
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
    }

    private void DarkModeToggle_Click(object sender, RoutedEventArgs e)
    {
        var darkMode = DarkModeToggle.IsChecked == true;
        ThemeManager.Apply(Application.Current, darkMode);
        _settings.DarkMode = darkMode;
        SaveSettings(
            darkMode ? "Dark mode enabled and saved." : "Light mode enabled and saved.");
    }

    private void SaveSettings(string successMessage)
    {
        try
        {
            ThemeManager.SaveSettings(_settings);
            UpdateStatus("Settings saved", successMessage, StatusKind.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            UpdateStatus(
                "Setting changed but not saved",
                $"This preference may reset next time the app starts. {ex.Message}",
                StatusKind.Error);
        }
    }

    private void AutoTypeToggle_Click(object sender, RoutedEventArgs e)
    {
        var enabled = AutoTypeToggle.IsChecked == true;
        UpdateStatus(
            enabled ? "Automatic typing enabled" : "Automatic typing disabled",
            enabled
                ? "New transcripts will be typed into the selected target."
                : "Review transcripts, then insert them when ready.",
            StatusKind.Ready);
    }

    private void HoldButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (StartRecording(RecordingMode.Hold))
        {
            HoldButton.CaptureMouse();
            e.Handled = true;
        }
    }

    private void HoldButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_recordingMode == RecordingMode.Hold)
        {
            StopRecording();
            HoldButton.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void HoldButton_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_recordingMode == RecordingMode.Hold)
        {
            StopRecording();
        }
    }

    private void HoldButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && StartRecording(RecordingMode.Hold))
        {
            e.Handled = true;
        }
    }

    private void HoldButton_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && _recordingMode == RecordingMode.Hold)
        {
            StopRecording();
            e.Handled = true;
        }
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e) => ToggleRecording();

    private void ToggleRecording()
    {
        if (_recordingMode == RecordingMode.Toggle)
        {
            StopRecording();
        }
        else if (_recordingMode == RecordingMode.None)
        {
            StartRecording(RecordingMode.Toggle);
        }
    }

    private bool StartRecording(RecordingMode mode)
    {
        if (_isProcessing || _recordingMode != RecordingMode.None)
        {
            return false;
        }

        if (ModelSelector.SelectedItem is not WhisperModel selectedModel)
        {
            UpdateStatus(
                "No Whisper model selected",
                "Add a ggml model .bin file to whisper_cpp\\models, then restart the app.",
                StatusKind.Error);
            return false;
        }

        if (InferenceSelector.SelectedItem is not WhisperExecutable selectedExecutable)
        {
            UpdateStatus(
                "No Whisper engine selected",
                "Select an available inference option in Settings.",
                StatusKind.Error);
            return false;
        }

        try
        {
            _recordingModelPath = selectedModel.Path;
            _recordingExecutable = selectedExecutable;
            if (MicrophoneSelector.SelectedItem is MicrophoneDevice microphone)
            {
                _recorder.DeviceNumber = microphone.DeviceNumber;
            }

            _recordingTargetWindow = _lastExternalWindow;
            ResetInputLevel();
            _recorder.Start();
            _recordingMode = mode;
            UpdateControls();
            UpdateStatus(
                "Listening…",
                _recordingTargetWindow == 0
                    ? "Recording audio. Select a target field before inserting."
                    : "Speak naturally. Release F8 or stop the recording to transcribe.",
                StatusKind.Recording);
            return true;
        }
        catch (Exception ex)
        {
            _recordingTargetWindow = 0;
            UpdateStatus("Microphone unavailable", ex.Message, StatusKind.Error);
            return false;
        }
    }

    private async void StopRecording()
    {
        if (_recordingMode == RecordingMode.None || _isProcessing)
        {
            return;
        }

        _recordingMode = RecordingMode.None;
        _isProcessing = true;
        ResetInputLevel();
        UpdateControls();
        UpdateStatus("Transcribing…", "Whisper is converting your speech to text.", StatusKind.Processing);

        var targetWindow = _recordingTargetWindow;
        var modelPath = _recordingModelPath;
        var executable = _recordingExecutable;
        var autoType = AutoTypeToggle.IsChecked == true;
        try
        {
            var recording = await _recorder.StopAsync();
            string transcript;
            try
            {
                if (recording.Duration < TimeSpan.FromMilliseconds(300))
                {
                    throw new InvalidOperationException(
                        "The recording was too short to transcribe. Hold F8 longer or use toggle recording.");
                }

                if (recording.Rms < 35 && recording.Peak < 150)
                {
                    throw new InvalidOperationException(
                        $"The recording contains almost no microphone signal " +
                        $"(average level {recording.Rms:F0}, peak {recording.Peak}). " +
                        "Choose the correct microphone and check its Windows input level.");
                }

                var transcriber = _transcriber
                    ?? throw new InvalidOperationException("The Whisper engine is not available.");
                transcript = await transcriber.TranscribeAsync(
                    recording.Path,
                    modelPath ?? throw new InvalidOperationException("No Whisper model is selected."),
                    executable ?? throw new InvalidOperationException("No Whisper inference device is selected."));
            }
            finally
            {
                TryDeleteAudio(recording.Path);
            }

            if (_isClosing)
            {
                return;
            }

            if (TranscriptBox.Text.Length > 0 &&
                !char.IsWhiteSpace(TranscriptBox.Text[^1]))
            {
                TranscriptBox.AppendText(Environment.NewLine);
            }

            TranscriptBox.AppendText(transcript);
            TranscriptBox.CaretIndex = TranscriptBox.Text.Length;
            TranscriptBox.ScrollToEnd();

            if (autoType && targetWindow != 0)
            {
                try
                {
                    await Task.Run(() => WindowTextTyper.TypeText(targetWindow, transcript));
                }
                catch (Exception ex)
                {
                    UpdateStatus(
                        "Transcript ready — typing failed",
                        $"{ex.Message} The transcript is still available above.",
                        StatusKind.Error);
                    return;
                }

                UpdateStatus("Done — text inserted", "Your transcript was typed into the target window.", StatusKind.Ready);
            }
            else
            {
                UpdateStatus(
                    "Transcription complete",
                    targetWindow == 0
                        ? "No target window was selected. Review the text, then select a field and insert it."
                        : "Review your transcript, then insert it whenever you’re ready.",
                    StatusKind.Ready);
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                UpdateStatus("Transcription failed", ex.Message, StatusKind.Error);
            }
        }
        finally
        {
            _isProcessing = false;
            if (!_isClosing)
            {
                UpdateControls();
            }
        }
    }

    private async void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        var text = TranscriptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            UpdateStatus("Nothing to insert", "Record a phrase first, or type into the transcript.", StatusKind.Error);
            return;
        }

        var targetWindow = _lastExternalWindow != 0
            ? _lastExternalWindow
            : _recordingTargetWindow;
        if (targetWindow == 0)
        {
            UpdateStatus("No target window", "Select a text field in another app, then try Insert again.", StatusKind.Error);
            return;
        }

        InsertButton.IsEnabled = false;
        try
        {
            await Task.Run(() => WindowTextTyper.TypeText(targetWindow, text));
            UpdateStatus("Text inserted", "The transcript was typed into the target window.", StatusKind.Ready);
        }
        catch (Exception ex)
        {
            UpdateStatus("Could not insert text", ex.Message, StatusKind.Error);
        }
        finally
        {
            InsertButton.IsEnabled = !_isProcessing;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TranscriptBox.Text))
        {
            UpdateStatus("Nothing to copy", "Record a phrase first, or type into the transcript.", StatusKind.Error);
            return;
        }

        try
        {
            Clipboard.SetText(TranscriptBox.Text);
            UpdateStatus("Copied to clipboard", "The transcript is ready to paste.", StatusKind.Ready);
        }
        catch (Exception ex)
        {
            UpdateStatus("Could not copy text", ex.Message, StatusKind.Error);
        }
    }

    private void TranscriptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var words = TranscriptBox.Text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
        WordCountText.Text = $"{words} {(words == 1 ? "word" : "words")}";
    }

    private void UpdateControls()
    {
        var busy = _isProcessing;
        HoldButton.IsEnabled = !busy && _recordingMode is RecordingMode.None or RecordingMode.Hold;
        ToggleButton.IsEnabled = !busy;
        InsertButton.IsEnabled = !busy;
        MicrophoneSelector.IsEnabled = !busy && _recordingMode == RecordingMode.None;
        ModelSelector.IsEnabled =
            !busy && _recordingMode == RecordingMode.None && ModelSelector.Items.Count > 0;
        InferenceSelector.IsEnabled =
            !busy && _recordingMode == RecordingMode.None && InferenceSelector.Items.Count > 0;
        ToggleLabel.Text = _recordingMode == RecordingMode.Toggle
            ? "STOP RECORDING"
            : "START RECORDING";
    }

    private void UpdateStatus(string title, string detail, StatusKind kind)
    {
        StatusText.Text = title;
        StatusDetailText.Text = detail;
        StatusDot.Fill = kind switch
        {
            StatusKind.Recording => new SolidColorBrush(Color.FromRgb(224, 76, 88)),
            StatusKind.Processing => (Brush)FindResource("Accent"),
            StatusKind.Error => new SolidColorBrush(Color.FromRgb(218, 142, 50)),
            _ => new SolidColorBrush(Color.FromRgb(72, 177, 126))
        };
    }

    private static void TryDeleteAudio(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _isClosing = true;
        _targetWindowTimer.Stop();
        _hotkeys?.Dispose();
        _recorder.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    private enum RecordingMode
    {
        None,
        Hold,
        Toggle
    }

    private enum StatusKind
    {
        Ready,
        Recording,
        Processing,
        Error
    }
}
