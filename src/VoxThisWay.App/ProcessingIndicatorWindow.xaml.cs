using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;

namespace VoxThisWay.App;

public partial class ProcessingIndicatorWindow : Window
{
    private readonly DispatcherTimer _followTimer;
    private readonly DispatcherTimer _successHideTimer;

    public ProcessingIndicatorWindow()
    {
        InitializeComponent();

        _followTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _followTimer.Tick += (_, _) => UpdatePosition();

        _successHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _successHideTimer.Tick += (_, _) =>
        {
            _successHideTimer.Stop();
            HideIndicator();
        };

        // Start hidden; App will control visibility.
        Hide();
    }

    public void ShowListening()
    {
        StopSuccessTimer();
        ListeningVisual.Visibility = Visibility.Visible;
        ProcessingVisual.Visibility = Visibility.Collapsed;
        SuccessVisual.Visibility = Visibility.Collapsed;
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
        StopSuccessTimer();
        ListeningVisual.Visibility = Visibility.Collapsed;
        ProcessingVisual.Visibility = Visibility.Visible;
        SuccessVisual.Visibility = Visibility.Collapsed;
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

    public void ShowSuccess()
    {
        StopSuccessTimer();
        ListeningVisual.Visibility = Visibility.Collapsed;
        ProcessingVisual.Visibility = Visibility.Collapsed;
        SuccessVisual.Visibility = Visibility.Visible;
        UpdatePosition();

        if (!_followTimer.IsEnabled)
        {
            _followTimer.Start();
        }

        if (!IsVisible)
        {
            Show();
        }

        _successHideTimer.Start();
    }

    public void HideIndicator()
    {
        StopSuccessTimer();
        if (_followTimer.IsEnabled)
        {
            _followTimer.Stop();
        }

        if (IsVisible)
        {
            Hide();
        }
    }

    private void StopSuccessTimer()
    {
        if (_successHideTimer.IsEnabled)
        {
            _successHideTimer.Stop();
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
