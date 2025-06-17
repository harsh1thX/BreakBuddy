using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinRT.Interop;

namespace BreakBuddy
{
    public sealed partial class BreakOverlayWindow : Window
    {
        // Explicit field declarations for XAML elements (if needed)
        private TextBlock BreakMessage => this.FindName("BreakMessage") as TextBlock;
        private TextBlock CountdownText => this.FindName("CountdownText") as TextBlock;
        private TextBlock EyeCareTip => this.FindName("EyeCareTip") as TextBlock;
        private ProgressBar BreakProgressBar => this.FindName("BreakProgressBar") as ProgressBar;
        private Button SkipButton => this.FindName("SkipButton") as Button;
        private Image BlurredBackground => this.FindName("BlurredBackground") as Image;
        private Grid RootGrid => this.FindName("RootGrid") as Grid;

        private DispatcherTimer _breakTimer;
        private int _breakDurationSeconds;
        private int _remainingSeconds;
        private bool _breakCompleted = false;
        private TaskCompletionSource<bool> _breakCompletionSource;

        private readonly string[] _eyeCareTips = {
            "Look at something 20 feet away for 20 seconds",
            "Blink slowly and deliberately to lubricate your eyes",
            "Close your eyes and relax for a moment",
            "Look around the room to exercise your eye muscles",
            "Focus on distant objects to relax eye strain",
            "Roll your eyes gently in circles",
            "Massage your temples lightly",
            "Take deep breaths and relax your shoulders"
        };

        // Win32 API declarations for screen capture
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        // System metrics constants
        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;

        public BreakOverlayWindow(int breakDurationSeconds)
        {
            this.InitializeComponent();

            _breakDurationSeconds = breakDurationSeconds;
            _remainingSeconds = breakDurationSeconds;
            _breakCompletionSource = new TaskCompletionSource<bool>();

            SetupWindow();
            CaptureAndBlurScreen();
            SetupBreakTimer();
            SetRandomEyeCareTip();

            // Setup keyboard handling
            this.Content.KeyDown += BreakOverlayWindow_KeyDown;
            this.Content.IsTabStop = true;
            this.Content.Focus(FocusState.Programmatic);
        }

        private void SetupWindow()
        {
            try
            {
                var appWindow = GetAppWindowForCurrentWindow();
                if (appWindow != null)
                {
                    // Make window fullscreen and always on top
                    appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

                    // Get screen dimensions using Win32 API
                    int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                    int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                    // Try to make it truly fullscreen
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(0, 0, screenWidth, screenHeight));
                }

                this.Title = "BreakBuddy - Eye Break";
                System.Diagnostics.Debug.WriteLine("BreakBuddy: Break overlay window setup complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up break window: {ex.Message}");
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        private void CaptureAndBlurScreen()
        {
            try
            {
                // Get screen dimensions using Win32 API
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                System.Diagnostics.Debug.WriteLine($"BreakBuddy: Capturing screen {screenWidth}x{screenHeight}");

                // Capture screen using Win32 API
                using (var bitmap = CaptureScreen(screenWidth, screenHeight))
                {
                    if (bitmap != null)
                    {
                        // Apply blur effect
                        var blurredBitmap = ApplyGaussianBlur(bitmap, 15);

                        // Convert to WinUI image
                        var writeableBitmap = ConvertToWriteableBitmap(blurredBitmap);
                        if (BlurredBackground != null)
                        {
                            BlurredBackground.Source = writeableBitmap;
                        }

                        blurredBitmap.Dispose();
                    }
                    else
                    {
                        // Fallback to solid color
                        SetFallbackBackground();
                    }
                }

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Screen captured and blurred successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error capturing screen: {ex.Message}");
                SetFallbackBackground();
            }
        }

        private Bitmap CaptureScreen(int width, int height)
        {
            try
            {
                // Get desktop DC
                IntPtr desktopDC = GetDC(IntPtr.Zero);

                // Create compatible DC and bitmap
                IntPtr memoryDC = CreateCompatibleDC(desktopDC);
                IntPtr bitmap = CreateCompatibleBitmap(desktopDC, width, height);

                // Select bitmap into memory DC
                IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

                // Copy screen to memory DC
                BitBlt(memoryDC, 0, 0, width, height, desktopDC, 0, 0, 0x00CC0020); // SRCCOPY

                // Create managed bitmap from HBITMAP
                var managedBitmap = Image.FromHbitmap(bitmap);

                // Cleanup
                SelectObject(memoryDC, oldBitmap);
                DeleteObject(bitmap);
                DeleteDC(memoryDC);
                ReleaseDC(IntPtr.Zero, desktopDC);

                return new Bitmap(managedBitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CaptureScreen: {ex.Message}");
                return null;
            }
        }

        private void SetFallbackBackground()
        {
            try
            {
                if (RootGrid != null)
                {
                    RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.DarkSlateGray);
                }
                System.Diagnostics.Debug.WriteLine("BreakBuddy: Using fallback background color");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting fallback background: {ex.Message}");
            }
        }

        private Bitmap ApplyGaussianBlur(Bitmap source, int radius)
        {
            try
            {
                var result = new Bitmap(source.Width, source.Height);

                // Simple box blur approximation (faster than true Gaussian)
                using (var graphics = Graphics.FromImage(result))
                {
                    graphics.DrawImage(source, 0, 0);
                }

                // Apply multiple passes of box blur for better effect
                for (int pass = 0; pass < 3; pass++)
                {
                    var temp = ApplyBoxBlur(result, radius / 3);
                    result.Dispose();
                    result = temp;
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying blur: {ex.Message}");
                // Return original if blur fails
                return new Bitmap(source);
            }
        }

        private Bitmap ApplyBoxBlur(Bitmap source, int radius)
        {
            if (radius <= 0) return new Bitmap(source);

            var result = new Bitmap(source.Width, source.Height);

            try
            {
                var sourceData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var resultData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* sourcePtr = (byte*)sourceData.Scan0;
                    byte* resultPtr = (byte*)resultData.Scan0;

                    int width = source.Width;
                    int height = source.Height;
                    int stride = sourceData.Stride;

                    // Simple box blur implementation
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int r = 0, g = 0, b = 0, count = 0;

                            for (int dy = -radius; dy <= radius; dy++)
                            {
                                for (int dx = -radius; dx <= radius; dx++)
                                {
                                    int nx = x + dx;
                                    int ny = y + dy;

                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                    {
                                        byte* pixel = sourcePtr + ny * stride + nx * 4;
                                        b += pixel[0];
                                        g += pixel[1];
                                        r += pixel[2];
                                        count++;
                                    }
                                }
                            }

                            if (count > 0)
                            {
                                byte* resultPixel = resultPtr + y * stride + x * 4;
                                resultPixel[0] = (byte)(b / count);
                                resultPixel[1] = (byte)(g / count);
                                resultPixel[2] = (byte)(r / count);
                                resultPixel[3] = 255;
                            }
                        }
                    }
                }

                source.UnlockBits(sourceData);
                result.UnlockBits(resultData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in box blur: {ex.Message}");
                result?.Dispose();
                return new Bitmap(source);
            }

            return result;
        }

        private WriteableBitmap ConvertToWriteableBitmap(Bitmap bitmap)
        {
            try
            {
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, ImageFormat.Png);
                    memory.Position = 0;

                    var writeableBitmap = new WriteableBitmap(bitmap.Width, bitmap.Height);
                    writeableBitmap.SetSource(memory.AsRandomAccessStream());
                    return writeableBitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error converting to WritableBitmap: {ex.Message}");
                // Return a blank bitmap
                return new WriteableBitmap(1920, 1080);
            }
        }

        private void SetRandomEyeCareTip()
        {
            try
            {
                var random = new Random();
                int index = random.Next(_eyeCareTips.Length);
                if (EyeCareTip != null)
                {
                    EyeCareTip.Text = _eyeCareTips[index];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting eye care tip: {ex.Message}");
                if (EyeCareTip != null)
                {
                    EyeCareTip.Text = "Take a moment to rest your eyes";
                }
            }
        }

        private void SetupBreakTimer()
        {
            _breakTimer = new DispatcherTimer();
            _breakTimer.Interval = TimeSpan.FromSeconds(1);
            _breakTimer.Tick += BreakTimer_Tick;

            // Initialize display
            UpdateCountdownDisplay();

            _breakTimer.Start();
            System.Diagnostics.Debug.WriteLine($"BreakBuddy: Break timer started for {_breakDurationSeconds} seconds");
        }

        private void BreakTimer_Tick(object sender, object e)
        {
            _remainingSeconds--;
            UpdateCountdownDisplay();

            // Show skip button in last 5 seconds
            if (_remainingSeconds <= 5 && _remainingSeconds > 0)
            {
                if (SkipButton != null)
                {
                    SkipButton.Visibility = Visibility.Visible;
                    SkipButton.Content = $"Press ESC to Skip ({_remainingSeconds}s remaining)";
                }
            }

            if (_remainingSeconds <= 0)
            {
                CompleteBreak();
            }
        }

        private void UpdateCountdownDisplay()
        {
            try
            {
                if (CountdownText != null)
                {
                    CountdownText.Text = _remainingSeconds.ToString();
                }

                // Update progress bar (0 to 100)
                if (BreakProgressBar != null)
                {
                    double progress = (double)(_breakDurationSeconds - _remainingSeconds) / _breakDurationSeconds * 100;
                    BreakProgressBar.Value = progress;
                }

                // Update break message based on time remaining
                if (BreakMessage != null)
                {
                    if (_remainingSeconds > 15)
                    {
                        BreakMessage.Text = "Look away from your screen and rest your eyes";
                    }
                    else if (_remainingSeconds > 5)
                    {
                        BreakMessage.Text = "Almost done! Keep resting your eyes";
                    }
                    else if (_remainingSeconds > 0)
                    {
                        BreakMessage.Text = "Great job! You can return to work soon";
                    }
                    else
                    {
                        BreakMessage.Text = "Break complete! You may now resume work";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating countdown: {ex.Message}");
            }
        }

        private void BreakOverlayWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape && _remainingSeconds <= 5)
            {
                System.Diagnostics.Debug.WriteLine("BreakBuddy: Break skipped by user (ESC key)");
                CompleteBreak();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_remainingSeconds <= 5)
            {
                System.Diagnostics.Debug.WriteLine("BreakBuddy: Break skipped by user (button click)");
                CompleteBreak();
            }
        }

        private void CompleteBreak()
        {
            if (_breakCompleted) return;

            try
            {
                _breakCompleted = true;
                _breakTimer?.Stop();

                System.Diagnostics.Debug.WriteLine("BreakBuddy: Break completed");

                _breakCompletionSource.SetResult(true);
                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error completing break: {ex.Message}");
                _breakCompletionSource.SetResult(false);
            }
        }

        public Task<bool> WaitForBreakCompleteAsync()
        {
            return _breakCompletionSource.Task;
        }
    }
}