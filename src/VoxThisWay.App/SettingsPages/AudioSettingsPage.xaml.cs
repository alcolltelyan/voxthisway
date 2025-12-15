using System;
using System.Windows;
using System.Windows.Controls;
using VoxThisWay.Core.Audio;

namespace VoxThisWay.App.SettingsPages;

public partial class AudioSettingsPage : Page
{
    private readonly SettingsSession _session;

    public AudioSettingsPage()
    {
        InitializeComponent();

        _session = VoxThisWay.App.SettingsWindow.CurrentSession
                   ?? throw new InvalidOperationException("Settings session is not available.");

        DeviceCombo.ItemsSource = _session.Devices;
        DeviceCombo.SelectedItem = _session.SelectedDevice;
    }

    private void DeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceCombo.SelectedItem is AudioDeviceInfo device)
        {
            _session.SelectedDevice = device;
        }
    }

    private async void TestDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_session.SelectedDevice is null)
        {
            var owner = Window.GetWindow(this);
            if (owner is null)
            {
                MessageBox.Show("Please select an input device first.", "Microphone test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(owner, "Please select an input device first.", "Microphone test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }

        TestButton.IsEnabled = false;
        try
        {
            var message = await _session.TestMicrophoneAsync(_session.SelectedDevice);
            var owner = Window.GetWindow(this);
            if (owner is null)
            {
                MessageBox.Show(message, "Microphone test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(owner, message, "Microphone test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            var owner = Window.GetWindow(this);
            var message = $"Microphone test failed: {ex.Message}";
            if (owner is null)
            {
                MessageBox.Show(message, "Microphone test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(owner, message, "Microphone test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }
}
