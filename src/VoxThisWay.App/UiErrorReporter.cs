using System;
using System.Windows;

namespace VoxThisWay.App;

internal static class UiErrorReporter
{
    public static void ShowError(string title, string message)
    {
        try
        {
            var app = Application.Current;
            var dispatcher = app?.Dispatcher;

            void Show()
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (dispatcher is null)
            {
                Show();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                Show();
            }
            else
            {
                dispatcher.BeginInvoke(new Action(Show));
            }
        }
        catch
        {
            // As a last resort, attempt a direct MessageBox call.
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
