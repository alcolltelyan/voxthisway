using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VoxThisWay.Core.Abstractions.Text;

namespace VoxThisWay.Services.Text;

public sealed class TextInjectionService : ITextInjectionService
{
    private readonly ILogger<TextInjectionService> _logger;
    private string _lastInjectedText = string.Empty;

    public TextInjectionService(ILogger<TextInjectionService> logger)
    {
        _logger = logger;
    }

    public Task InjectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.CompletedTask;
        }

        var diffText = CalculateDiff(text);
        if (string.IsNullOrEmpty(diffText))
        {
            _logger.LogDebug("Text injection skipped; no diff to apply. Length={Length}.", text.Length);
            return Task.CompletedTask;
        }

        _logger.LogDebug("Injecting text. TotalLength={TotalLength}, DiffLength={DiffLength}, DiffPreview=\"{Preview}\"", text.Length, diffText.Length, Truncate(diffText, 100));

        try
        {
            SendKeys(diffText);
            _lastInjectedText = text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text injection failed during SendInput.");
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        _lastInjectedText = string.Empty;
    }

    private string CalculateDiff(string currentText)
    {
        var src = _lastInjectedText;
        var minLength = Math.Min(src.Length, currentText.Length);
        var index = 0;

        while (index < minLength && src[index] == currentText[index])
        {
            index++;
        }

        return currentText[index..];
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, maxLength) + "…";
    }

    private void SendKeys(string text)
    {
        foreach (var chunk in ChunkByWord(text))
        {
            var inputs = chunk.SelectMany(ToInputs).ToArray();
            if (inputs.Length == 0)
            {
                continue;
            }

            var requested = (uint)inputs.Length;
            var remaining = requested;
            var offset = 0;
            var attempt = 0;

            while (remaining > 0 && attempt < 3)
            {
                var sent = SendInput(remaining, inputs[offset..], Marshal.SizeOf(typeof(INPUT)));
                if (sent == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger.LogError(
                        "SendInput failed for chunk. Requested={Requested}, Win32Error={Error}, ChunkPreview=\"{Preview}\"",
                        remaining,
                        error,
                        Truncate(chunk, 50));
                    throw new InvalidOperationException("SendInput failed.");
                }

                if (sent < remaining)
                {
                    _logger.LogWarning(
                        "SendInput injected fewer events than requested. Requested={Requested}, Injected={Injected}, ChunkPreview=\"{Preview}\"",
                        remaining,
                        sent,
                        Truncate(chunk, 50));
                }

                offset += (int)sent;
                remaining -= sent;
                attempt++;
            }

            if (remaining > 0)
            {
                _logger.LogError(
                    "SendInput could not inject all events after retries. Remaining={Remaining}, ChunkPreview=\"{Preview}\"",
                    remaining,
                    Truncate(chunk, 50));
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<string> ChunkByWord(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var sb = new StringBuilder();
        foreach (var c in text)
        {
            sb.Append(c);
            if (char.IsWhiteSpace(c))
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

    private static INPUT[] ToInputs(char c)
    {
        var utf16 = Encoding.Unicode.GetBytes(new[] { c });
        var scan = BitConverter.ToUInt16(utf16, 0);

        // For KEYEVENTF_UNICODE we must send both key-down and key-up events
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;

        return new[]
        {
            new INPUT
            {
                type = 1,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scan,
                        dwFlags = KEYEVENTF_UNICODE
                    }
                }
            },
            new INPUT
            {
                type = 1,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scan,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                    }
                }
            }
        };
    }

    #region Win32 Interop

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        // Union must be large enough to hold mouse, keyboard, or hardware input.
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    #endregion
}
