using System;
using System.IO;

namespace VoxThisWay.Core.Configuration;

public sealed class WhisperLocalOptions
{
    public string ExecutablePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "Speech", "whisper_cli.exe");

    public string ModelPath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "Speech", "Models", "ggml-tiny.en.bin");

    public bool UseGpu { get; set; } = true;

    /// <summary>
    /// Minimum chunk duration in milliseconds before invoking Whisper.
    /// </summary>
    public int ChunkDurationMilliseconds { get; set; } = 3000;

    public string? AdditionalArguments { get; set; }

    public string ResolveExecutablePath() => Environment.ExpandEnvironmentVariables(ExecutablePath);

    public string ResolveModelPath() => Environment.ExpandEnvironmentVariables(ModelPath);
}
