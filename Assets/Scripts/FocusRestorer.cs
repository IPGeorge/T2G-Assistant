using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace T2G.Assistant
{
    public static class ExponentialBackoffFocusRestorer
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private static IntPtr _appWindowHandle;

        public static bool IsRestoringFocus { get; private set; } = false;

        public static void Initialize()
        {
            _appWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
        }

        public static bool IsFocusedWindow()
        {
            return (GetForegroundWindow() == _appWindowHandle);
        }


        /// <summary>
        /// Exponential backoff - delays increase with each retry
        /// </summary>
        public static async Task<bool> RestoreFocusWithExponentialBackoff(
            int maxAttempts = 8,
            int initialDelayMs = 100,
            double backoffFactor = 1.5,
            int maxDelayMs = 3000)
        {
            if (_appWindowHandle == IntPtr.Zero)
                Initialize();

            IsRestoringFocus = true;
            int currentDelay = initialDelayMs;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Restore window if minimized
                ShowWindowAsync(_appWindowHandle, SW_RESTORE);
                ShowWindowAsync(_appWindowHandle, SW_SHOW);

                // Try to set foreground
                SetForegroundWindow(_appWindowHandle);

                // Check if we got focus
                await Task.Delay(100); // Small verification delay
                if (GetForegroundWindow() == _appWindowHandle)
                {
                    IsRestoringFocus = false;
                    return true;
                }

                if (attempt < maxAttempts)
                {
                    // Exponential backoff: delay increases each time
                    Console.WriteLine($"Attempt {attempt} failed, waiting {currentDelay}ms before next try");
                    await Task.Delay(currentDelay);

                    // Increase delay exponentially (with cap)
                    currentDelay = Math.Min((int)(currentDelay * backoffFactor), maxDelayMs);
                }
            }

            IsRestoringFocus = false;
            return false;
        }
    }
}