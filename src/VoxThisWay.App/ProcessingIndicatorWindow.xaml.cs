using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;

namespace VoxThisWay.App;

public partial class ProcessingIndicatorWindow : Window
{
    private readonly DispatcherTimer _followTimer;

    public ProcessingIndicatorWindow()
    {
        InitializeComponent();

        _followTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _followTimer.Tick += (_, _) => UpdatePosition();

        // Start hidden; App will control visibility.
        Hide();
    }

    public void ShowListening()
    {
        ListeningVisual.Visibility = Visibility.Visible;
        ProcessingVisual.Visibility = Visibility.Collapsed;
        UpdatePosition();

        if (!_followTimer.IsEnabled)
        {
            _followTimer.Start();
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    public void ShowProcessing()
    {
        ListeningVisual.Visibility = Visibility.Collapsed;
        ProcessingVisual.Visibility = Visibility.Visible;
        UpdatePosition();

        if (!_followTimer.IsEnabled)
        {
            _followTimer.Start();
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideIndicator()
    {
        if (_followTimer.IsEnabled)
        {
            _followTimer.Stop();
        }

        if (IsVisible)
        {
            Hide();
        }
    }

    private void UpdatePosition()
    {
        try
        {
            var cursor = System.Windows.Forms.Cursor.Position; // screen coordinates in pixels
            var dpi = VisualTreeHelper.GetDpi(this);

            const double offsetDip = 8; // keep close without covering the cursor
            Left = (cursor.X / dpi.DpiScaleX) + offsetDip;
            Top = (cursor.Y / dpi.DpiScaleY) + offsetDip;
        }
        catch
        {
            // Swallow any transient failures getting cursor position; not critical.
        }
    }
}
