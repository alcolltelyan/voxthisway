using System;
using System.Windows;
using System.Windows.Forms;
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

    public void Start()
    {
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

    public void Stop()
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
            const int offset = 16; // offset so we don't cover the cursor exactly
            Left = cursor.X + offset;
            Top = cursor.Y + offset;
        }
        catch
        {
            // Swallow any transient failures getting cursor position; not critical.
        }
    }
}
