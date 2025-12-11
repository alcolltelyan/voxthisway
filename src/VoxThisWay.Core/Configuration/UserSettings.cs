using System.Text.Json.Serialization;

namespace VoxThisWay.Core.Configuration;

public sealed class UserSettings
{
    /// <summary>
    /// Identifier of the preferred audio input device. If null or empty, the system default
    /// communications capture device will be used.
    /// </summary>
    [JsonPropertyName("audioInputDeviceId")]
    public string? AudioInputDeviceId { get; set; }

    [JsonPropertyName("hotkeyVirtualKey")]
    public int HotkeyVirtualKey { get; set; } = 0x20; // VK_SPACE

    [JsonPropertyName("hotkeyUseCtrl")]
    public bool HotkeyUseCtrl { get; set; } = true;

    [JsonPropertyName("hotkeyUseAlt")]
    public bool HotkeyUseAlt { get; set; }

    [JsonPropertyName("hotkeyUseShift")]
    public bool HotkeyUseShift { get; set; }

    [JsonPropertyName("hotkeyUseWin")]
    public bool HotkeyUseWin { get; set; }

    [JsonPropertyName("speechEngine")]
    public SpeechEngineKind? SpeechEngine { get; set; }
}
