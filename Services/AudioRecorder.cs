using System.IO;
using NAudio.Wave;

namespace Verbal.Services;

internal sealed class AudioRecorder : IDisposable
{
    private const int SampleRate = 16_000;
    private readonly object _sync = new();
    private WaveInEvent? _capture;
    private WaveFileWriter? _writer;
    private TaskCompletionSource<AudioRecordingResult>? _recordingStopped;
    private string? _recordingPath;
    private double _sumSquares;
    private long _sampleCount;
    private int _peak;

    public int DeviceNumber { get; set; } = -1;

    public event Action<float>? InputLevelChanged;

    public static IReadOnlyList<MicrophoneDevice> GetDevices()
    {
        var devices = new List<MicrophoneDevice>
        {
            new(-1, "Windows default microphone")
        };
        for (var index = 0; index < WaveIn.DeviceCount; index++)
        {
            var capabilities = WaveIn.GetCapabilities(index);
            devices.Add(new MicrophoneDevice(index, capabilities.ProductName));
        }

        return devices;
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_capture is not null)
            {
                throw new InvalidOperationException("A recording is already in progress.");
            }

            var path = Path.Combine(
                Path.GetTempPath(),
                $"whisper-voice-{Guid.NewGuid():N}.wav");
            WaveInEvent? capture = null;
            WaveFileWriter? writer = null;
            try
            {
                writer = new WaveFileWriter(path, new WaveFormat(SampleRate, 16, 1));
                capture = new WaveInEvent
                {
                    DeviceNumber = DeviceNumber,
                    WaveFormat = new WaveFormat(SampleRate, 16, 1),
                    BufferMilliseconds = 100
                };
                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;
                _recordingPath = path;
                _recordingStopped = new TaskCompletionSource<AudioRecordingResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _sumSquares = 0;
                _sampleCount = 0;
                _peak = 0;
                _writer = writer;
                _capture = capture;
                capture.StartRecording();
            }
            catch
            {
                capture?.Dispose();
                writer?.Dispose();
                TryDelete(path);
                _capture = null;
                _writer = null;
                _recordingPath = null;
                _recordingStopped = null;
                throw;
            }
        }
    }

    public Task<AudioRecordingResult> StopAsync()
    {
        lock (_sync)
        {
            if (_capture is null || _recordingStopped is null)
            {
                throw new InvalidOperationException("There is no active recording.");
            }

            _capture.StopRecording();
            return _recordingStopped.Task;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        double chunkSumSquares = 0;
        var chunkSampleCount = args.BytesRecorded / sizeof(short);
        var chunkPeak = 0;
        for (var offset = 0; offset + 1 < args.BytesRecorded; offset += sizeof(short))
        {
            var sample = (int)BitConverter.ToInt16(args.Buffer, offset);
            var magnitude = Math.Abs(sample);
            chunkPeak = Math.Max(chunkPeak, magnitude);
            chunkSumSquares += (double)sample * sample;
        }

        lock (_sync)
        {
            _writer?.Write(args.Buffer, 0, args.BytesRecorded);
            _sumSquares += chunkSumSquares;
            _sampleCount += chunkSampleCount;
            _peak = Math.Max(_peak, chunkPeak);
        }

        var rms = chunkSampleCount == 0
            ? 0
            : Math.Sqrt(chunkSumSquares / chunkSampleCount);
        var decibels = rms == 0
            ? 0
            : (float)Math.Clamp((20 * Math.Log10(rms / short.MaxValue) + 60) / 60 * 100, 0, 100);
        InputLevelChanged?.Invoke(decibels);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        TaskCompletionSource<AudioRecordingResult>? completion;
        string? path;
        double rms;
        int peak;
        long sampleCount;
        lock (_sync)
        {
            completion = _recordingStopped;
            path = _recordingPath;
            rms = _sampleCount == 0 ? 0 : Math.Sqrt(_sumSquares / _sampleCount);
            peak = _peak;
            sampleCount = _sampleCount;
            _writer?.Dispose();
            _writer = null;
            _capture?.Dispose();
            _capture = null;
            _recordingStopped = null;
            _recordingPath = null;
        }

        if (args.Exception is not null)
        {
            if (path is not null)
            {
                TryDelete(path);
            }

            completion?.TrySetException(args.Exception);
        }
        else if (path is not null)
        {
            completion?.TrySetResult(new AudioRecordingResult(
                path,
                TimeSpan.FromSeconds((double)sampleCount / SampleRate),
                rms,
                peak));
        }
        else
        {
            completion?.TrySetException(
                new InvalidOperationException("The recording file was not created."));
        }
    }

    private static void TryDelete(string path)
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

    public void Dispose()
    {
        lock (_sync)
        {
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
            _writer?.Dispose();
            _writer = null;
            if (_recordingPath is not null)
            {
                TryDelete(_recordingPath);
            }

            _recordingPath = null;
            _recordingStopped?.TrySetCanceled();
            _recordingStopped = null;
        }
    }
}

internal sealed record MicrophoneDevice(int DeviceNumber, string Name)
{
    public override string ToString() => Name;
}

internal sealed record AudioRecordingResult(
    string Path,
    TimeSpan Duration,
    double Rms,
    int Peak);
