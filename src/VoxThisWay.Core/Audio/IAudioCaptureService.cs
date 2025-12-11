using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VoxThisWay.Core.Audio;

public interface IAudioCaptureService : IDisposable
{
    IReadOnlyList<AudioDeviceInfo> EnumerateInputDevices();

    Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default);

    Task StopAsync();

    event EventHandler<AudioBufferReadyEventArgs>? AudioBufferReady;
}
