using System.IO;
using System.Diagnostics;

namespace Verbal.Services;

internal sealed class WhisperTranscriber(WhisperInstallation installation)
{
    public async Task<string> TranscribeAsync(
        string audioPath,
        string modelPath)
    {
        var executablePath = installation.GetCpuExecutablePath()
            ?? throw new FileNotFoundException(
                "Could not find the CPU Whisper CLI in whisper_cpp\\cpu.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)
                ?? installation.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--no-gpu");
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
