// MiddlewareTool/Services/AppSettingsReplacer.cs
using System;
using System.IO;

namespace MiddlewareTool.Services
{
    public class AppSettingsReplacer
    {
        private static readonly string TARGET_FILE = "appsettings.json";

        /// <summary>
        /// Ghi đè file "appsettings.json" trong thư mục của một file .exe cụ thể
        /// bằng một file template.
        /// </summary>
        /// <param name="targetExePath">Đường dẫn đến file .exe (ví dụ: client.exe hoặc server.exe)</param>
        /// <param name="templatePath">Đường dẫn đến file template appsettings.json</param>
        public void ReplaceSetting(string targetExePath, string templatePath)
        {
            // Kiểm tra đầu vào cơ bản
            if (string.IsNullOrEmpty(targetExePath) || !File.Exists(targetExePath))
            {
                Console.WriteLine($"Lỗi: Không tìm thấy file exe: {targetExePath}");
                return;
            }

            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                Console.WriteLine($"Lỗi: Không tìm thấy file template: {templatePath}");
                return;
            }

            // Lấy thư mục chứa file .exe
            string destDir = Path.GetDirectoryName(targetExePath);

            // Gọi hàm CopyFile (logic này đã đúng và không cần thay đổi)
            CopyFile(templatePath, destDir);
        }

        /// <summary>
        /// Hàm private thực hiện việc tìm và sao chép.
        /// </summary>
        private void CopyFile(string template, string destDir)
        {
            if (string.IsNullOrEmpty(destDir) || !Directory.Exists(destDir))
            {
                Console.WriteLine($"Lỗi: Thư mục đích không tồn tại: {destDir}");
                return;
            }

            // Tìm tất cả các file appsettings.json trong thư mục (và thư mục con)
            string[] searchResults = Directory.GetFiles(destDir, TARGET_FILE, SearchOption.AllDirectories);

            if (searchResults.Length == 0)
            {
                Console.WriteLine($"Cảnh báo: Không tìm thấy file '{TARGET_FILE}' trong: {destDir}");
            }

            foreach (string item in searchResults)
            {
                try
                {
                    // Ghi đè file
                    File.Copy(template, item, true);
                    Console.WriteLine($"Đã thay thế: {item} bằng {template}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi copy file: {ex.Message}");
                }
            }
        }
    }
}