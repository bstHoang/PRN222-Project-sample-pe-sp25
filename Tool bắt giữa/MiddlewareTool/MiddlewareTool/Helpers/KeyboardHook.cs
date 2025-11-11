// MiddlewareTool/Helpers/KeyboardHook.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiddlewareTool.Helpers
{
    public static class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static Action? _onEnterPressed;
        private static Action? _onCapturePressed;
        private static int _clientProcessId = 0;

        public static void SetHook(Action onEnterPressed, Action onCapturePressed, int clientProcessId)
        {
            _onEnterPressed = onEnterPressed;
            _onCapturePressed = onCapturePressed;
            _clientProcessId = clientProcessId;
            _hookID = SetHook(_proc);
        }

        public static void Unhook()
        {
            UnhookWindowsHookEx(_hookID);
            _onEnterPressed = null;
            _onCapturePressed = null;
            _clientProcessId = 0;
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                // Check if the key is Enter or F12
                bool isEnter = vkCode == 0x0D; // VK_RETURN
                bool isF12 = vkCode == 0x7B;   // VK_F12
                
                if (isEnter || isF12)
                {
                    // Check if the foreground window belongs to the client process
                    bool isClientWindow = false;
                    if (_clientProcessId > 0)
                    {
                        IntPtr foreground = GetForegroundWindow();
                        if (foreground != IntPtr.Zero)
                        {
                            GetWindowThreadProcessId(foreground, out uint pid);
                            isClientWindow = (pid == (uint)_clientProcessId);
                        }
                    }
                    
                    // If this is the client window, process the key for MiddlewareTool
                    if (isClientWindow)
                    {
                        if (isEnter)
                        {
                            _onEnterPressed?.Invoke();
                            // Don't suppress Enter - let it pass through so Console.ReadLine() works
                        }
                        else if (isF12)
                        {
                            _onCapturePressed?.Invoke();
                            // Suppress F12 so it doesn't reach ConsoleManager's monitoring thread
                            // This prevents interference with ConsoleManager's F12 handling
                            return (IntPtr)1;
                        }
                    }
                }
            }
            
            // Pass the key through to the next hook or target application
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}