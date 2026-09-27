using System.IO;

namespace Verbal.Services;

internal sealed record WhisperInstallation(
    string ModelsDirectory,
    string WorkingDirectory)
{
    public IReadOnlyList<WhisperExecutable> GetExecutables()
    {
        var executables = new List<WhisperExecutable>();
        var cpuExecutable = GetCpuExecutablePath();
        if (cpuExecutable is not null)
        {
            executables.Add(new WhisperExecutable(
                WhisperBackend.Cpu,
                cpuExecutable,
                Path.GetDirectoryName(cpuExecutable) ?? WorkingDirectory));
        }

        var cudaDirectory = Path.Combine(WorkingDirectory, "cuda");
        var cudaExecutable = Path.Combine(cudaDirectory, "whisper-cli.exe");
        if (File.Exists(cudaExecutable))
        {
            executables.Add(new WhisperExecutable(
                WhisperBackend.Cuda,
                cudaExecutable,
                cudaDirectory));
        }

        return executables;
    }

    public static WhisperInstallation Find()
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };

        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                var whisperDirectory = Path.Combine(directory.FullName, "whisper_cpp");
                var modelsDirectory = Path.Combine(whisperDirectory, "models");
                var installation = new WhisperInstallation(modelsDirectory, whisperDirectory);
                if (installation.GetCpuExecutablePath() is not null)
                {
                    return installation;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            "Could not find the CPU Whisper CLI (whisper_cpp\\whisper-cli.exe). " +
            "Keep the whisper_cpp folder beside the app or in one of its parent folders.");
    }

    public string? GetCpuExecutablePath()
    {
        var cpuExecutable = Path.Combine(WorkingDirectory, "whisper-cli.exe");
        return File.Exists(cpuExecutable) ? cpuExecutable : null;
    }

    public IReadOnlyList<WhisperModel> GetModels()
    {
        if (!Directory.Exists(ModelsDirectory))
        {
            return [];
        }

        var models = Directory.EnumerateFiles(ModelsDirectory, "ggml-*.bin")
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return !name.Contains("vad", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("silero", StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => new WhisperModel(Path.GetFileName(path), path))
            .OrderBy(model => model.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return models;
    }
}

internal enum WhisperBackend
{
    Cpu,
    Cuda
}

internal sealed record WhisperExecutable(
    WhisperBackend Backend,
    string Path,
    string WorkingDirectory)
{
    public string DisplayName => Backend switch
    {
        WhisperBackend.Cpu => "CPU",
        WhisperBackend.Cuda => "CUDA (NVIDIA GPU)",
        _ => throw new InvalidOperationException($"Unsupported Whisper backend: {Backend}.")
    };

    public override string ToString() => DisplayName;
}

internal sealed record WhisperModel(string FileName, string Path)
{
    public string DisplayName
    {
        get
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(FileName);
            if (name.StartsWith("ggml-", StringComparison.OrdinalIgnoreCase))
            {
                name = name[5..];
            }

            name = name.Replace(".en", " (English)", StringComparison.OrdinalIgnoreCase)
                .Replace('.', ' ')
                .Replace('-', ' ');
            return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length == 0
                    ? part
                    : char.ToUpperInvariant(part[0]) + part[1..]));
        }
    }

    public override string ToString() => DisplayName;
}
