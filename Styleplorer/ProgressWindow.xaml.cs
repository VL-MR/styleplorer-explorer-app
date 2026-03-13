using System;
using System.Windows;

namespace Styleplorer
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(string status, int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                Title = status;
                StatusText.Text = status;
                ProgressBar.Value = percentage;
                ProgressText.Text = $"{percentage}%";
            });
        }
    }
}
