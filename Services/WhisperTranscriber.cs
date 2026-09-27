using System.Diagnostics;

namespace Verbal.Services;

internal sealed class WhisperTranscriber
{
    public async Task<string> TranscribeAsync(
        string audioPath,
        string modelPath,
        WhisperExecutable executable)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable.Path,
            WorkingDirectory = executable.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (executable.Backend == WhisperBackend.Cpu)
        {
            startInfo.ArgumentList.Add("--no-gpu");
        }

        startInfo.ArgumentList.Add("--no-prints");
        startInfo.ArgumentList.Add("--no-timestamps");
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(modelPath);
        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(audioPath);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start whisper-cli.exe.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var output = (await standardOutput.ConfigureAwait(false)).Trim();
        var error = (await standardError.ConfigureAwait(false)).Trim();

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException(
                $"Whisper exited with code {process.ExitCode}: {details}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("Whisper did not detect any speech.");
        }

        return output;
    }
}
