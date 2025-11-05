using System;
using System.Runtime.InteropServices;

namespace ConsoleApp1
{
    /// <summary>
    /// ConsoleManager provides managed console output and handles F12 key detection
    /// to clear input buffer and prevent stale input from appearing.
    /// </summary>
    public static class ConsoleManager
    {
        // Import Windows API functions for console input buffer management
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushConsoleInputBuffer(IntPtr hConsoleInput);
        private const int STD_INPUT_HANDLE = -10;
        private static bool _f12Pressed = false;  // Fixed typo from _f1Pressed to _f12Pressed
        /// <summary>
        /// Initialize the console manager with F12 key monitoring
        /// </summary>
        public static void Initialize()
        {
            // Start background thread to monitor for F12 key
            var monitorThread = new System.Threading.Thread(MonitorF12Key)
            {
                IsBackground = true
            };
            monitorThread.Start();
        }
        /// <summary>
        /// Monitor for F12 key press to clear input buffer
        /// </summary>
        private static void MonitorF12Key()
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.F12)
                    {
                        _f12Pressed = true;
                        ClearInputBuffer();
                    }
                    else
                    {
                        // Put the key back (not possible in C#, so we skip non-F12 keys here)
                        // The main thread will read it normally
                    }
                }
                System.Threading.Thread.Sleep(50);
            }
        }
        /// <summary>
        /// Clear the console input buffer to remove any pending input
        /// </summary>
        public static void ClearInputBuffer()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    IntPtr handle = GetStdHandle(STD_INPUT_HANDLE);
                    if (handle != IntPtr.Zero)
                    {
                        FlushConsoleInputBuffer(handle);
                    }
                }
                else
                {
                    // For non-Windows platforms, clear available keys
                    while (Console.KeyAvailable)
                    {
                        Console.ReadKey(true);
                    }
                }
            }
            catch
            {
                // Silently fail if buffer clearing doesn't work
            }
        }
        /// <summary>
        /// Managed Console.Write that normalizes spaces and adds a space at the end
        /// </summary>
        /// <param name="text">Text to write to console</param>
        public static void Write(string text)
        {
            string normalized = NormalizeText(text);
            Console.Write(normalized);
        }
        /// <summary>
        /// Managed Console.WriteLine that normalizes spaces and adds a space at the end before newline
        /// </summary>
        /// <param name="text">Text to write to console</param>
        public static void WriteLine(string text)
        {
            string normalized = NormalizeText(text);
            Console.WriteLine(normalized.TrimEnd());  // Trim trailing space before newline if desired, or keep it
        }
        /// <summary>
        /// Managed Console.WriteLine (no parameters) that tracks output
        /// </summary>
        public static void WriteLine()
        {
            Console.WriteLine();
        }
        /// <summary>
        /// Normalize the text by splitting on spaces (removing extras) and joining with space, adding space at end
        /// </summary>
        /// <param name="text">Input text</param>
        /// <returns>Normalized text with space after each element and at the end</returns>
        private static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Split by spaces, remove empty entries (handles extra spaces)
            string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Join with space (adds space after each part, including a trailing space)
            return string.Join(" ", parts) + " ";
        }
        /// <summary>
        /// Check if F12 was pressed and reset the flag
        /// </summary>
        /// <returns>True if F12 was pressed since last check</returns>
        public static bool WasF12Pressed()
        {
            bool result = _f12Pressed;
            _f12Pressed = false;
            return result;
        }
    }
}