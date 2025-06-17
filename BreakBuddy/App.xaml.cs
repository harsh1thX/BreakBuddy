using Microsoft.UI.Xaml;
using System.Collections.Generic;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Drawing;

namespace BreakBuddy
{
    public partial class App : Application
    {
        private Window m_window;
        private TaskbarIcon _trayIcon;
        private MenuFlyout _trayMenu;
        private MenuFlyoutItem _pauseMenuItem;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            SetupSystemTray();

            m_window.Activate();
        }

        private void SetupSystemTray()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("BreakBuddy: Setting up system tray...");

                _trayIcon = new TaskbarIcon
                {
                    ToolTipText = "BreakBuddy - Eye Protection"
                };

                // Try to set icon using the correct property
                try
                {
                    var iconPath = System.IO.Path.Combine(
                        System.AppDomain.CurrentDomain.BaseDirectory,
                        "Assets", "icon.ico");

                    if (System.IO.File.Exists(iconPath))
                    {
                        // Load icon using System.Drawing.Icon
                        var icon = new Icon(iconPath);
                        _trayIcon.Icon = icon;
                        System.Diagnostics.Debug.WriteLine("BreakBuddy: Icon loaded successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("BreakBuddy: Icon file not found, using default");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"BreakBuddy: Could not load icon: {ex.Message}");
                }

                CreateTrayContextMenu();

                // Handle left clicks to show window
                _trayIcon.LeftClickCommand = new RelayCommand(() =>
                {
                    System.Diagnostics.Debug.WriteLine("BreakBuddy: Tray icon clicked - showing window");
                    ShowMainWindow();
                });

                // Show the tray icon
                _trayIcon.ForceCreate();

                System.Diagnostics.Debug.WriteLine("BreakBuddy: System tray setup complete");

                // Test notification after a short delay
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    ShowBalloonTip("BreakBuddy Started", "Eye protection app is ready to use!");
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up system tray: {ex.Message}");
            }
        }

        private void CreateTrayContextMenu()
        {
            try
            {
                _trayMenu = new MenuFlyout();

                // Open/Show window item
                var openItem = new MenuFlyoutItem
                {
                    Text = "🏠 Open BreakBuddy"
                };
                openItem.Click += (s, e) => ShowMainWindow();

                // Pause/Resume protection item
                _pauseMenuItem = new MenuFlyoutItem
                {
                    Text = "▶️ Start Protection"
                };
                _pauseMenuItem.Click += (s, e) => ToggleProtection();

                // Separator
                var separator1 = new MenuFlyoutSeparator();

                // Exit item
                var exitItem = new MenuFlyoutItem
                {
                    Text = "❌ Exit BreakBuddy"
                };
                exitItem.Click += (s, e) => ExitApplication();

                // Add items to menu
                _trayMenu.Items.Add(openItem);
                _trayMenu.Items.Add(_pauseMenuItem);
                _trayMenu.Items.Add(separator1);
                _trayMenu.Items.Add(exitItem);

                _trayIcon.ContextMenuMode = H.NotifyIcon.ContextMenuMode.PopupMenu;
                _trayIcon.ContextFlyout = _trayMenu;

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Context menu created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating context menu: {ex.Message}");
            }
        }

        private void ShowMainWindow()
        {
            try
            {
                if (m_window is MainWindow mainWindow)
                {
                    mainWindow.ShowFromTray();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing main window: {ex.Message}");
            }
        }

        private void ToggleProtection()
        {
            try
            {
                if (m_window is MainWindow mainWindow)
                {
                    mainWindow.ToggleProtection();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling protection: {ex.Message}");
            }
        }

        private void ExitApplication()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("BreakBuddy: Exiting application");
                _trayIcon?.Dispose();
                this.Exit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exiting application: {ex.Message}");
            }
        }

        public void UpdateTrayStatus(string status, bool isPaused)
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.ToolTipText = $"BreakBuddy - {status}";
                }

                if (_pauseMenuItem != null)
                {
                    if (!isPaused)
                    {
                        var mainWindow = m_window as MainWindow;
                        if (mainWindow != null && mainWindow.IsProtectionRunning)
                        {
                            _pauseMenuItem.Text = "⏸️ Pause Protection";
                        }
                        else
                        {
                            _pauseMenuItem.Text = "▶️ Start Protection";
                        }
                    }
                    else
                    {
                        _pauseMenuItem.Text = "▶️ Resume Protection";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating tray status: {ex.Message}");
            }
        }

        public void UpdateTrayTooltip(string tooltip)
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.ToolTipText = tooltip;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating tray tooltip: {ex.Message}");
            }
        }

        public void ShowBalloonTip(string title, string message)
        {
            try
            {
                if (_trayIcon != null)
                {
                    // Use the correct method name for H.NotifyIcon version 2.3.0
                    _trayIcon.ShowNotification(
                        title: title,
                        message: message,
                        icon: H.NotifyIcon.NotificationIcon.Info,
                        largeIcon: false,
                        sound: true,
                        respectQuietTime: false,
                        realtime: false,
                        timeout: TimeSpan.FromSeconds(3)
                    );
                    System.Diagnostics.Debug.WriteLine($"BreakBuddy: Notification shown - {title}: {message}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    // Fallback method without optional parameters
                    _trayIcon?.ShowNotification(title, message);
                    System.Diagnostics.Debug.WriteLine($"BreakBuddy: Notification shown (fallback) - {title}: {message}");
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing notification: {ex.Message}, Fallback error: {ex2.Message}");

                    // Ultimate fallback - just log the notification
                    System.Diagnostics.Debug.WriteLine($"BreakBuddy Notification: {title} - {message}");
                }
            }
        }

        public void ClearNotifications()
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.ClearNotifications();
                    System.Diagnostics.Debug.WriteLine("BreakBuddy: Notifications cleared");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing notifications: {ex.Message}");
            }
        }

        public bool IsSystemTrayAvailable()
        {
            try
            {
                return _trayIcon != null;
            }
            catch
            {
                return false;
            }
        }
    }

    // Simple RelayCommand implementation for handling tray clicks
    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}