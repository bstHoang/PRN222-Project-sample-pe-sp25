using System;
using System.IO;

namespace MiddlewareTool.Services
{
    public class AppSettingsReplacer
    {
        private static readonly string TARGET_FILE = "appsettings.json";

        public void ReplaceSetting(string targetExePath, string templatePath)
        {
            if (string.IsNullOrEmpty(targetExePath) || !File.Exists(targetExePath))
            {
                Console.WriteLine($"Error: Exe file not found: {targetExePath}");
                return;
            }

            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                Console.WriteLine($"Error: File not found: {templatePath}");
                return;
        }

            string destDir = Path.GetDirectoryName(targetExePath);

            CopyFile(templatePath, destDir);
        }

        private void CopyFile(string template, string destDir)
        {
            if (string.IsNullOrEmpty(destDir) || !Directory.Exists(destDir))
            {
                Console.WriteLine($"Error: Destination directory does not exist: {destDir}");
                return;
            }

            string[] searchResults = Directory.GetFiles(destDir, TARGET_FILE, SearchOption.AllDirectories);

            if (searchResults.Length == 0)
            {
                Console.WriteLine($"Warning: File '{TARGET_FILE}' not found in: {destDir}");
            }

            foreach (string item in searchResults)
            {
                try
                {
                    File.Copy(template, item, true);
                    Console.WriteLine($"Replaced: {item} with {template}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error copying file: {ex.Message}");
                }
            }
        }
    }
}