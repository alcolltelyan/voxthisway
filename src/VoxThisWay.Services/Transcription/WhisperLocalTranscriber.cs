using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Transcription;

namespace VoxThisWay.Services.Transcription;

public sealed class WhisperLocalTranscriber : ISpeechTranscriber
{
    private readonly WhisperLocalOptions _options;
    private readonly ILogger<WhisperLocalTranscriber> _logger;
    private readonly object _bufferLock = new();
    private readonly SemaphoreSlim _chunkProcessingLock = new(1, 1);
    private MemoryStream _buffer = new();
    private AudioFormat _currentFormat;
    private TranscriptionConfig? _config;
    private int _chunkBytesThreshold;
    private bool _isRunning;
    private int _chunkCounter;

    public WhisperLocalTranscriber(IOptions<WhisperLocalOptions> options, ILogger<WhisperLocalTranscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string EngineName => "whisper-local";

    public event EventHandler<TranscriptSegment>? TranscriptAvailable;

    public Task StartAsync(TranscriptionConfig config, CancellationToken cancellationToken = default)
    {
        _config = config;
        _currentFormat = config.InputFormat;
        _chunkBytesThreshold = CalculateChunkSizeBytes(_currentFormat, _options.ChunkDurationMilliseconds);
        _buffer.Dispose();
        _buffer = new MemoryStream();
        _isRunning = true;
        _logger.LogInformation("Whisper local transcriber initialized.");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        byte[]? remaining = null;
        long remainingLength;
        lock (_bufferLock)
        {
            remainingLength = _buffer.Length;
            if (_buffer.Length > 0)
            {
                remaining = _buffer.ToArray();
                _buffer.SetLength(0);
            }
        }

        _logger.LogDebug(
            "Whisper StopAsync invoked. RemainingBufferBytes={RemainingBytes}, ThresholdBytes={ThresholdBytes}",
            remainingLength,
            _chunkBytesThreshold);

        if (remaining is not null)
        {
            _logger.LogDebug("Whisper StopAsync flushing remaining buffer. Bytes={Bytes}", remaining.Length);
            await _chunkProcessingLock.WaitAsync(CancellationToken.None);
            try
            {
                await ProcessChunkAsync(remaining, _currentFormat, CancellationToken.None);
            }
            finally
            {
                _chunkProcessingLock.Release();
            }
        }
        else
        {
            _logger.LogDebug("Whisper StopAsync: no remaining buffer to flush.");
        }
    }

    public async Task PushAudioAsync(ReadOnlyMemory<byte> buffer, AudioFormat format, CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        byte[]? chunk = null;
        lock (_bufferLock)
        {
            _buffer.Write(buffer.Span);
            if (_buffer.Length >= _chunkBytesThreshold)
            {
                chunk = _buffer.ToArray();
                _buffer.SetLength(0);
            }
        }

        if (chunk is not null)
        {
            await _chunkProcessingLock.WaitAsync(cancellationToken);
            try
            {
                await ProcessChunkAsync(chunk, format, cancellationToken);
            }
            finally
            {
                _chunkProcessingLock.Release();
            }
        }
    }

    private async Task ProcessChunkAsync(byte[] chunk, AudioFormat format, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "ProcessChunkAsync invoked. ChunkBytes={ChunkBytes}, HasConfig={HasConfig}",
            chunk.Length,
            _config is not null);

        if (chunk.Length == 0 || _config is null)
        {
            _logger.LogWarning(
                "Skipping Whisper chunk due to invalid state. ChunkBytes={ChunkBytes}, HasConfig={HasConfig}",
                chunk.Length,
                _config is not null);
            return;
        }
        string? wavPath = null;
        string? outputPrefix = null;
        string? outputPath = null;
        try
        {
            var chunkId = Interlocked.Increment(ref _chunkCounter);
            outputPrefix = Path.Combine(AppDirectories.TempDirectory, $"chunk_{chunkId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");
            wavPath = $"{outputPrefix}.wav";
            outputPath = $"{outputPrefix}.txt";

            _logger.LogDebug("Writing WAV file for chunk {ChunkId}. Path={Path}, Bytes={Bytes}", chunkId, wavPath, chunk.Length);
            WriteWaveFile(wavPath, chunk, format);
            _logger.LogDebug("WAV file written for chunk {ChunkId}", chunkId);
            var execPath = _options.ResolveExecutablePath();

            if (!File.Exists(execPath))
            {
                throw new FileNotFoundException($"Whisper executable not found at {execPath}");
            }

            if (!File.Exists(_options.ResolveModelPath()))
            {
                throw new FileNotFoundException($"Whisper model not found at {_options.ResolveModelPath()}");
            }

            var args = BuildArguments(wavPath, outputPrefix, _config.Language);

            _logger.LogDebug(
                "Preparing Whisper process for chunk {ChunkId}. ExecPath={ExecPath}, Args={Args}",
                chunkId,
                execPath,
                args);
            var psi = new ProcessStartInfo(execPath, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stdout.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderr.AppendLine(e.Data);
                }
            };

            _logger.LogDebug("Starting Whisper process for chunk {ChunkId}. Bytes={Bytes}, WavPath={WavPath}", chunkId, chunk.Length, wavPath);

            var startTime = DateTimeOffset.UtcNow;
            process.Start();
            _logger.LogDebug(
                "Whisper process started for chunk {ChunkId}. Pid={Pid}",
                chunkId,
                process.Id);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogDebug(
                "Whisper process exited for chunk {ChunkId}. ExitCode={ExitCode}, DurationMs={DurationMs}",
                chunkId,
                process.ExitCode,
                elapsed.TotalMilliseconds);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Whisper exited with code {Code}. stderr: {Error}", process.ExitCode, stderr.ToString());
            }

            _logger.LogDebug("Reading transcript for chunk {ChunkId}. OutputPath={OutputPath}", chunkId, outputPath ?? string.Empty);
            var rawTranscript = await ReadTranscriptAsync(outputPath ?? string.Empty, stdout.ToString(), cancellationToken);
            var isBlankAudio = rawTranscript?.IndexOf("[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase) >= 0;
            var transcriptText = NormalizeTranscript(rawTranscript);

            _logger.LogDebug(
                "Whisper chunk {ChunkId} completed. ExitCode={ExitCode}, TranscriptLength={Length}", chunkId, process.ExitCode, transcriptText?.Length ?? 0);

            if (!string.IsNullOrWhiteSpace(transcriptText))
            {
                TranscriptAvailable?.Invoke(
                    this,
                    new TranscriptSegment(
                        transcriptText.Trim(),
                        true,
                        TimeSpan.Zero,
                        TimeSpan.Zero));
                _logger.LogDebug("TranscriptAvailable raised for chunk {ChunkId}. Preview=\"{Preview}\"", chunkId, Truncate(transcriptText, 200));
            }
            else
            {
                if (isBlankAudio)
                {
                    _logger.LogDebug(
                        "Whisper chunk {ChunkId} contained blank audio.",
                        chunkId);
                }
                else
                {
                    _logger.LogWarning(
                        "Whisper chunk {ChunkId} produced no text. StdoutPreview=\"{Stdout}\", StderrPreview=\"{Stderr}\"",
                        chunkId,
                        Truncate(stdout.ToString(), 500),
                        Truncate(stderr.ToString(), 500));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful stop
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Whisper chunk.");
        }
        finally
        {
            if (wavPath is not null && File.Exists(wavPath))
            {
                TryDelete(wavPath);
            }

            if (outputPath is not null && File.Exists(outputPath))
            {
                TryDelete(outputPath);
            }
        }
    }

    private string BuildArguments(string wavPath, string outputPrefix, string language)
    {
        var sb = new StringBuilder();
        sb.Append($"--model \"{_options.ResolveModelPath()}\" ");
        sb.Append($"--language {language} ");
        sb.Append("--output-txt ");
        sb.Append($"--output-file \"{outputPrefix}\" ");
        if (!string.IsNullOrWhiteSpace(_options.AdditionalArguments))
        {
            sb.Append(_options.AdditionalArguments);
            sb.Append(' ');
        }

        sb.Append($"\"{wavPath}\"");
        return sb.ToString();
    }

    private static void WriteWaveFile(string path, byte[] pcmData, AudioFormat format)
    {
        using var writer = new WaveFileWriter(path, new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels));
        writer.Write(pcmData, 0, pcmData.Length);
    }

    private static int CalculateChunkSizeBytes(AudioFormat format, int durationMs)
    {
        var bytesPerSample = format.BitsPerSample / 8;
        var samplesPerChunk = (int)(format.SampleRate * (durationMs / 1000.0));
        return Math.Max(bytesPerSample * format.Channels * samplesPerChunk, 1);
    }

    private static string? NormalizeTranscript(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            if (line.StartsWith("[") && line.Contains("]"))
            {
                var closeIdx = line.IndexOf(']');
                if (closeIdx >= 0 && closeIdx + 1 < line.Length)
                {
                    line = line[(closeIdx + 1)..].TrimStart();
                }
            }

            lines[i] = line;
        }

        var normalized = string.Join(" ", lines).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, "[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, maxLength) + "…";
    }

    private static async Task<string?> ReadTranscriptAsync(string outputPath, string stdout, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
        {
            return await File.ReadAllTextAsync(outputPath, cancellationToken);
        }

        return string.IsNullOrWhiteSpace(stdout) ? null : stdout;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _buffer.Dispose();
        _chunkProcessingLock.Dispose();
    }
}
