// MiddlewareTool/Services/AppSettingsReplacer.cs
using System;
using System.IO;

namespace MiddlewareTool.Services
{
    public class AppSettingsReplacer
    {
        private static readonly string TARGET_FILE = "appsettings.json";

        public void ReplaceAppSettings(string serverExePath, string serverTemplatePath, string clientExePath, string clientTemplatePath)
        {
            string serverDestDir = Path.GetDirectoryName(serverExePath);
            string clientDestDir = Path.GetDirectoryName(clientExePath);
            CopyFile(serverTemplatePath, serverDestDir);
            CopyFile(clientTemplatePath, clientDestDir);
        }

        private void CopyFile(string template, string destDir)
        {
            if (string.IsNullOrEmpty(template) || !File.Exists(template) ||
                string.IsNullOrEmpty(destDir) || !Directory.Exists(destDir))
            {
                return;
            }
            string[] searchResults = Directory.GetFiles(destDir, TARGET_FILE, SearchOption.AllDirectories);
            foreach (string item in searchResults)
            {
                try
                {
                    File.Copy(template, item, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}