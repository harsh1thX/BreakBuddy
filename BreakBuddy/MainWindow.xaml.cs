using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WinRT.Interop;

namespace BreakBuddy
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherTimer _intervalTimer;
        private DispatcherTimer _countdownTimer;
        private bool _isProtectionRunning = false;
        private bool _isProtectionPaused = false;
        private bool _isWindowHidden = false;
        private DateTime _nextBreakTime;
        private int _remainingSeconds;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "BreakBuddy - Eye Protection";

            // Set window size
            var appWindow = GetAppWindowForCurrentWindow();
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(600, 700));
            }

            InitializeTimers();
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        private void InitializeTimers()
        {
            // Timer for break intervals
            _intervalTimer = new DispatcherTimer();
            _intervalTimer.Tick += IntervalTimer_Tick;

            // Timer for countdown display
            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += CountdownTimer_Tick;
        }

        private void QuickInterval_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagValue)
            {
                if (int.TryParse(tagValue, out int minutes))
                {
                    IntervalMinutesInput.Value = minutes;
                }
            }
        }

        private void QuickDuration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagValue)
            {
                if (int.TryParse(tagValue, out int seconds))
                {
                    BreakDurationInput.Value = seconds;
                }
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int intervalMinutes = (int)IntervalMinutesInput.Value;
                int breakDuration = (int)BreakDurationInput.Value;

                if (intervalMinutes <= 0 || breakDuration <= 0)
                {
                    ShowErrorDialog("Please enter valid values for interval and break duration.");
                    return;
                }

                // Calculate next break time
                _nextBreakTime = DateTime.Now.AddMinutes(intervalMinutes);

                // Setup and start interval timer
                _intervalTimer.Interval = TimeSpan.FromMinutes(intervalMinutes);
                _intervalTimer.Start();

                // Start countdown timer for display
                _countdownTimer.Start();

                _isProtectionRunning = true;
                _isProtectionPaused = false;

                // Update UI
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                MinimizeToTrayButton.IsEnabled = true;
                IntervalMinutesInput.IsEnabled = false;
                BreakDurationInput.IsEnabled = false;

                StatusText.Text = $"Eye protection active - Break every {intervalMinutes} minutes";

                // Update system tray
                var app = (App)Application.Current;
                app.UpdateTrayStatus("Eye Protection Active", false);

                System.Diagnostics.Debug.WriteLine($"BreakBuddy: Protection started - {intervalMinutes}min intervals, {breakDuration}s breaks");
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Error starting protection: {ex.Message}");
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopProtection();
        }

        private void StopProtection()
        {
            try
            {
                _intervalTimer.Stop();
                _countdownTimer.Stop();
                _isProtectionRunning = false;
                _isProtectionPaused = false;

                // Update UI
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                MinimizeToTrayButton.IsEnabled = false;
                IntervalMinutesInput.IsEnabled = true;
                BreakDurationInput.IsEnabled = true;

                StatusText.Text = "Eye protection stopped";
                TimeRemainingText.Text = "--:--";

                // Update system tray
                var app = (App)Application.Current;
                app.UpdateTrayStatus("Eye Protection Stopped", false);

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Protection stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping protection: {ex.Message}");
            }
        }

        private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            HideToSystemTray();
        }

        private void HideToSystemTray()
        {
            try
            {
                this.Hide();
                _isWindowHidden = true;

                var app = (App)Application.Current;
                app.ShowBalloonTip("BreakBuddy Minimized",
                    "BreakBuddy is running in the background. Eye protection continues!");

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Hidden to system tray");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hiding to tray: {ex.Message}");
            }
        }

        public void ShowFromTray()
        {
            try
            {
                this.Show();
                this.Activate();
                _isWindowHidden = false;

                var appWindow = GetAppWindowForCurrentWindow();
                if (appWindow != null)
                {
                    appWindow.Show();
                }

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Restored from system tray");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing from tray: {ex.Message}");
            }
        }

        private void CountdownTimer_Tick(object sender, object e)
        {
            if (_isProtectionRunning && !_isProtectionPaused)
            {
                var timeRemaining = _nextBreakTime - DateTime.Now;

                if (timeRemaining.TotalSeconds > 0)
                {
                    int totalSeconds = (int)timeRemaining.TotalSeconds;
                    int minutes = totalSeconds / 60;
                    int seconds = totalSeconds % 60;

                    TimeRemainingText.Text = $"{minutes:D2}:{seconds:D2}";

                    // Update system tray tooltip
                    var app = (App)Application.Current;
                    app.UpdateTrayTooltip($"BreakBuddy - Next break in {minutes:D2}:{seconds:D2}");
                }
                else
                {
                    TimeRemainingText.Text = "Break Time!";
                }
            }
        }

        private async void IntervalTimer_Tick(object sender, object e)
        {
            if (!_isProtectionRunning || _isProtectionPaused) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("BreakBuddy: Break time triggered!");

                // Stop the interval timer temporarily
                _intervalTimer.Stop();

                // Create and show break overlay
                int breakDuration = (int)BreakDurationInput.Value;
                var breakWindow = new BreakOverlayWindow(breakDuration);
                breakWindow.Activate();

                // Wait for break to complete
                await breakWindow.WaitForBreakCompleteAsync();

                // Restart the timer for next interval if protection is still running
                if (_isProtectionRunning && !_isProtectionPaused)
                {
                    int intervalMinutes = (int)IntervalMinutesInput.Value;
                    _nextBreakTime = DateTime.Now.AddMinutes(intervalMinutes);
                    _intervalTimer.Start();

                    System.Diagnostics.Debug.WriteLine($"BreakBuddy: Next break scheduled for {_nextBreakTime:HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during break: {ex.Message}");

                // Restart timer even if there was an error
                if (_isProtectionRunning && !_isProtectionPaused)
                {
                    int intervalMinutes = (int)IntervalMinutesInput.Value;
                    _nextBreakTime = DateTime.Now.AddMinutes(intervalMinutes);
                    _intervalTimer.Start();
                }
            }
        }

        public void ToggleProtection()
        {
            if (!_isProtectionRunning)
            {
                StartButton_Click(null, null);
            }
            else if (_isProtectionPaused)
            {
                ResumeProtection();
            }
            else
            {
                PauseProtection();
            }
        }

        private void PauseProtection()
        {
            if (_isProtectionRunning && !_isProtectionPaused)
            {
                _intervalTimer.Stop();
                _countdownTimer.Stop();
                _isProtectionPaused = true;

                StatusText.Text = "Eye protection paused";
                TimeRemainingText.Text = "PAUSED";

                var app = (App)Application.Current;
                app.UpdateTrayStatus("Eye Protection Paused", true);
                app.ShowBalloonTip("Protection Paused", "Eye protection has been paused.");

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Protection paused");
            }
        }

        private void ResumeProtection()
        {
            if (_isProtectionRunning && _isProtectionPaused)
            {
                _intervalTimer.Start();
                _countdownTimer.Start();
                _isProtectionPaused = false;

                int intervalMinutes = (int)IntervalMinutesInput.Value;
                StatusText.Text = $"Eye protection resumed - Break every {intervalMinutes} minutes";

                var app = (App)Application.Current;
                app.UpdateTrayStatus("Eye Protection Active", false);
                app.ShowBalloonTip("Protection Resumed", "Eye protection has been resumed.");

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Protection resumed");
            }
        }

        private async void ShowErrorDialog(string message)
        {
            try
            {
                ContentDialog dialog = new ContentDialog()
                {
                    Title = "BreakBuddy",
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing dialog: {ex.Message}");
            }
        }

        public bool IsProtectionRunning => _isProtectionRunning;
        public bool IsProtectionPaused => _isProtectionPaused;
    }
}