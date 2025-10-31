// MiddlewareTool/Services/ConsoleCaptureService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MiddlewareTool.Services
{
    public class ConsoleCaptureService
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public short wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;
        }

        private const int STD_OUTPUT_HANDLE = -11;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ReadConsoleOutputCharacter(IntPtr hConsoleOutput, [Out] StringBuilder lpCharacter, uint nLength, COORD dwReadCoord, out uint lpNumberOfCharsRead);

        public string CaptureConsoleOutput(int processId)
        {
            if (processId == 0) return string.Empty;
            if (!AttachConsole((uint)processId))
            {
                return string.Empty; // Failed to attach, return empty
            }
            try
            {
                IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdHandle == INVALID_HANDLE_VALUE)
                {
                    return string.Empty;
                }
                CONSOLE_SCREEN_BUFFER_INFO csbi;
                if (!GetConsoleScreenBufferInfo(stdHandle, out csbi))
                {
                    return string.Empty;
                }
                short width = csbi.dwSize.X;
                short height = csbi.dwSize.Y;
                var lines = new List<string>();
                for (short y = 0; y < height; y++)
                {
                    COORD coord = new COORD { X = 0, Y = y };
                    StringBuilder sb = new StringBuilder(width);
                    uint numRead;
                    if (ReadConsoleOutputCharacter(stdHandle, sb, (uint)width, coord, out numRead))
                    {
                        string line = sb.ToString(0, (int)numRead).TrimEnd();
                        lines.Add(line);
                    }
                }
                // Remove trailing empty lines
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                {
                    lines.RemoveAt(lines.Count - 1);
                }
                return string.Join(Environment.NewLine, lines);
            }
            finally
            {
                FreeConsole();
            }
        }

        // === THAY ĐỔI CHỮ KÝ (SIGNATURE) CỦA HÀM NÀY ===
        public string ProcessClientConsoleOutput(
            string fullOutput,
            List<(string Line, DateTime Timestamp)> enterLines,
            List<string> possiblePrompts, // <-- THAM SỐ MỚI TỪ FILE CẤU HÌNH
            out List<(int Stage, string Input, string Timestamp)> stageInputs)
        {
            stageInputs = new List<(int Stage, string Input, string Timestamp)>();
            if (string.IsNullOrWhiteSpace(fullOutput))
            {
                return string.Empty;
            }
            // Split into lines
            string[] lines = fullOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            List<string> linesList = lines.ToList();
            while (linesList.Count > 0 && string.IsNullOrWhiteSpace(linesList[linesList.Count - 1]))
            {
                linesList.RemoveAt(linesList.Count - 1);
            }

            // === XOÁ BỎ HOÀN TOÀN KHỐI LOGIC "HỌC" TỰ ĐỘNG NÀY ===
            // // Collect possible prompts: lines that look like prompts (end with ': ' or ':')
            // HashSet<string> possiblePrompts = new HashSet<string>();
            // foreach (string l in linesList)
            // {
            //    ...
            // }
            // === KẾT THÚC PHẦN XOÁ BỎ ===

            // Build cleanedLines: for each line, if it matches an enterLine, replace with the prompt part
            List<string> cleanedLines = new List<string>();
            foreach (string l in linesList)
            {
                string trimmedL = l.Trim();
                var matchedEnterItem = enterLines.FirstOrDefault(e => e.Line.Trim() == trimmedL);
                string matchedEnter = matchedEnterItem.Line;

                if (!string.IsNullOrEmpty(matchedEnter))
                {
                    // === SỬA LOGIC TRÍCH XUẤT NÀY ===
                    // Find the prompt by finding the longest matching prompt from possiblePrompts
                    string prompt = possiblePrompts
                        .Where(p => matchedEnter.StartsWith(p))
                        .OrderByDescending(p => p.Length)
                        .FirstOrDefault(); // <-- XOÁ PHẦN '?? ... LastIndexOf ...'

                    if (prompt != null)
                    {
                        cleanedLines.Add(l.Replace(trimmedL, prompt)); // Replace with prompt
                    }
                    else
                    {
                        cleanedLines.Add(l); // Không tìm thấy prompt, giữ nguyên dòng
                    }
                    // === KẾT THÚC SỬA ĐỔI ===
                }
                else
                {
                    cleanedLines.Add(l);
                }
            }
            // Extract user inputs with stages
            for (int i = 0; i < enterLines.Count; i++)
            {
                string enterLine = enterLines[i].Line;
                DateTime timestamp = enterLines[i].Timestamp;

                // === SỬA LOGIC TRÍCH XUẤT NÀY ===
                // Find the longest matching prompt
                string prompt = possiblePrompts
                    .Where(p => enterLine.StartsWith(p))
                    .OrderByDescending(p => p.Length)
                    .FirstOrDefault(); // <-- XOÁ PHẦN '?? ... LastIndexOf ...'

                string input;
                if (prompt != null)
                {
                    // Nếu TÌM THẤY prompt -> Tách input
                    input = enterLine.Substring(prompt.Length).Trim();
                }
                else
                {
                    // Nếu KHÔNG TÌM THẤY -> Ghi lại cả dòng thô
                    input = enterLine;
                }
                // === KẾT THÚC SỬA ĐỔI ===

                stageInputs.Add((i + 1, input, timestamp.ToString("HH:mm:ss.fff")));
            }
            return string.Join(Environment.NewLine, cleanedLines);
        }
    }
}