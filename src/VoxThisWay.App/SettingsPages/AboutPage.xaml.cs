using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace VoxThisWay.App.SettingsPages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        VersionText.Text = "VoxThisWay — v0.1.2";
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
