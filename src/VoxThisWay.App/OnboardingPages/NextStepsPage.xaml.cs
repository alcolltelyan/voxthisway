using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace VoxThisWay.App.OnboardingPages;

public partial class NextStepsPage : Page
{
    public NextStepsPage()
    {
        InitializeComponent();
    }

    private void SupportLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
        }

        e.Handled = true;
    }
}
