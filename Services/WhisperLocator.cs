using System.IO;

namespace Verbal.Services;

internal sealed record WhisperInstallation(
    string ModelsDirectory,
    string WorkingDirectory)
{
    public IReadOnlyList<WhisperExecutable> GetExecutables()
    {
        var executables = new List<WhisperExecutable>();
        var cpuDirectory = Path.Combine(WorkingDirectory, "cpu");
        var cpuSubfolderExecutable = Path.Combine(cpuDirectory, "whisper-cli.exe");
        if (File.Exists(cpuSubfolderExecutable))
        {
            executables.Add(new WhisperExecutable(
                WhisperBackend.Cpu,
                cpuSubfolderExecutable,
                cpuDirectory));
        }
        else
        {
            var cpuExecutable = Path.Combine(WorkingDirectory, "whisper-cli-cpu.exe");
            if (File.Exists(cpuExecutable))
            {
                executables.Add(new WhisperExecutable(
                    WhisperBackend.Cpu,
                    cpuExecutable,
                    WorkingDirectory));
            }
            else
            {
                cpuExecutable = Path.Combine(WorkingDirectory, "whisper-cli.exe");
                if (File.Exists(cpuExecutable))
                {
                    executables.Add(new WhisperExecutable(
                        WhisperBackend.Cpu,
                        cpuExecutable,
                        WorkingDirectory));
                }
            }
        }

        var cudaDirectory = Path.Combine(WorkingDirectory, "cuda");
        var cudaExecutable = Path.Combine(cudaDirectory, "whisper-cli.exe");
        if (File.Exists(cudaExecutable))
        {
            executables.Add(new WhisperExecutable(WhisperBackend.Cuda, cudaExecutable, cudaDirectory));
        }
        else
        {
            cudaExecutable = Path.Combine(WorkingDirectory, "whisper-cli-cuda.exe");
            if (File.Exists(cudaExecutable))
            {
                executables.Add(new WhisperExecutable(
                    WhisperBackend.Cuda,
                    cudaExecutable,
                    WorkingDirectory));
            }
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
                if (installation.GetExecutables().Count > 0)
                {
                    return installation;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            "Could not find a Whisper CLI executable (whisper-cli.exe, whisper-cli-cpu.exe, " +
            "or whisper-cli-cuda.exe) in the whisper_cpp folder or its cpu/cuda subfolders. " +
            "Keep the whisper_cpp folder beside the app or in one of its parent folders.");
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
