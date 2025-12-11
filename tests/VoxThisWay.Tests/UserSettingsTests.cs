using System;
using System.Text.Json;
using VoxThisWay.Core.Configuration;
using Xunit;

namespace VoxThisWay.Tests;

public class UserSettingsTests
{
    [Fact]
    public void Hotkey_HasExpectedDefaults()
    {
        var settings = new UserSettings();

        Assert.Equal(0x20, settings.HotkeyVirtualKey); // VK_SPACE
        Assert.True(settings.HotkeyUseCtrl);
        Assert.False(settings.HotkeyUseAlt);
        Assert.False(settings.HotkeyUseShift);
        Assert.False(settings.HotkeyUseWin);
    }

    [Fact]
    public void Settings_RoundTrip_ThroughJson()
    {
        var original = new UserSettings
        {
            AudioInputDeviceId = "device-123",
            HotkeyVirtualKey = 0x78, // F9
            HotkeyUseCtrl = false,
            HotkeyUseAlt = false,
            HotkeyUseShift = false,
            HotkeyUseWin = false,
            SpeechEngine = SpeechEngineKind.Azure,
            ShowOnboarding = false
        };

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<UserSettings>(json)!;

        Assert.Equal(original.AudioInputDeviceId, roundTripped.AudioInputDeviceId);
        Assert.Equal(original.HotkeyVirtualKey, roundTripped.HotkeyVirtualKey);
        Assert.Equal(original.HotkeyUseCtrl, roundTripped.HotkeyUseCtrl);
        Assert.Equal(original.HotkeyUseAlt, roundTripped.HotkeyUseAlt);
        Assert.Equal(original.HotkeyUseShift, roundTripped.HotkeyUseShift);
        Assert.Equal(original.HotkeyUseWin, roundTripped.HotkeyUseWin);
        Assert.Equal(original.SpeechEngine, roundTripped.SpeechEngine);
        Assert.Equal(original.ShowOnboarding, roundTripped.ShowOnboarding);
    }

    [Fact]
    public void Settings_Json_DoesNotAccidentallyContainSecretsPlaceholder()
    {
        var settings = new UserSettings();
        var json = JsonSerializer.Serialize(settings);

        // This is a coarse guardrail: UserSettings should not grow obvious secret fields.
        Assert.DoesNotContain("azureKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }
}
