using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MiddlewareTool.Services;  // Thêm using này
using MiddlewareTool.Helpers;  // Thêm using này
using MiddlewareTool.Models;   // Thêm using này

namespace MiddlewareTool
{
    public partial class MainWindow : Window
    {
        private const int PROXY_PORT = 5000;
        private const int REAL_SERVER_PORT = 5001;
        private string _excelLogPath = "";
        private Process? _serverProcess;
        private Process? _clientProcess;
        private CancellationTokenSource? _cts;
        private bool _isSessionRunning = false;
        public ObservableCollection<LoggedRequest> LoggedRequests { get; set; }
        private List<string> _enterLines = new List<string>();
        private string _clientLogDir = "";

        private ProxyService _proxyService;
        private ExcelLogger _excelLogger;
        private ConsoleCaptureService _consoleCaptureService;
        private AppSettingsReplacer _appSettingsReplacer;

        public MainWindow()
        {
            InitializeComponent();
            LoggedRequests = new ObservableCollection<LoggedRequest>();
            RequestsGrid.ItemsSource = LoggedRequests;

            _excelLogger = new ExcelLogger();  // Khởi tạo _excelLogger trước
            _proxyService = new ProxyService(PROXY_PORT, REAL_SERVER_PORT, LoggedRequests, _excelLogger);  // Sau đó mới khởi tạo _proxyService
            _consoleCaptureService = new ConsoleCaptureService();
            _appSettingsReplacer = new AppSettingsReplacer();
        }

        #region File Dialog Handlers
        private void BrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe" };
            if (openFileDialog.ShowDialog() == true)
            {
                ServerExePath.Text = openFileDialog.FileName;
            }
        }

        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe" };
            if (openFileDialog.ShowDialog() == true)
            {
                ClientExePath.Text = openFileDialog.FileName;
            }
        }

        private void AppsettingTemplate_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                AppSettingTemplate.Text = openFileDialog.FileName;
            }
        }

        private void AppsettingClientTemplate_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                AppSettingClientTemplate.Text = openFileDialog.FileName;
            }
        }

        private void SelectLogFile_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Select Excel file to save logs",
                FileName = "LogData.xlsx"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                _excelLogPath = saveFileDialog.FileName;
                ExcelLogPath.Text = _excelLogPath;
            }
        }
        #endregion

        #region Session Control
        private async void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_isSessionRunning)
            {
                StopSession();
            }
            else
            {
                if (string.IsNullOrEmpty(ServerExePath.Text) ||
                    string.IsNullOrEmpty(ClientExePath.Text) ||
                    string.IsNullOrEmpty(AppSettingTemplate.Text) ||
                    string.IsNullOrEmpty(AppSettingClientTemplate.Text) ||
                    string.IsNullOrEmpty(_excelLogPath))
                {
                    MessageBox.Show("Please provide all required paths, including the log file location.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                await StartSessionAsync();
            }
        }

        private async Task StartSessionAsync()
        {
            _appSettingsReplacer.ReplaceAppSettings(ServerExePath.Text, AppSettingTemplate.Text, ClientExePath.Text, AppSettingClientTemplate.Text);
            StartStopButton.Content = "Stop Grading Session";
            _isSessionRunning = true;
            LoggedRequests.Clear();
            _excelLogger.SetupExcelLogFile(_excelLogPath);
            _serverProcess = new Process { StartInfo = new ProcessStartInfo(ServerExePath.Text, REAL_SERVER_PORT.ToString()) { UseShellExecute = true } };
            _serverProcess.Start();
            _cts = new CancellationTokenSource();
            string selectedProtocol = (ProtocolSelection.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "HTTP";
            _proxyService.StartProxy(selectedProtocol, _cts.Token);
            await Task.Delay(1500);
            _clientProcess = new Process { StartInfo = new ProcessStartInfo(ClientExePath.Text) { UseShellExecute = true } };
            _clientProcess.Start();
            await Task.Delay(500); // Wait for client window to open
            _clientLogDir = Path.GetDirectoryName(_excelLogPath) ?? "";
            _enterLines.Clear();
            KeyboardHook.SetHook(OnEnterPressed);
        }

        private void OnEnterPressed()
        {
            if (_clientProcess == null || _clientProcess.HasExited) return;
            IntPtr foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid != (uint)_clientProcess.Id) return; // Not client window
            string currentOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);
            if (string.IsNullOrEmpty(currentOutput)) return;
            string[] lines = currentOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                string lastLine = lines.Last().Trim();
                if (!string.IsNullOrEmpty(lastLine) && !_enterLines.Contains(lastLine))
                {
                    _enterLines.Add(lastLine);
                }
            }
        }

        private void StopSession()
        {
            KeyboardHook.Unhook();
            _cts?.Cancel();
            _proxyService.StopProxy();
            string serverLogFile = Path.Combine(_clientLogDir, "ServerConsoleLog.txt");
            string clientLogFile = Path.Combine(_clientLogDir, "ClientConsoleLog.txt");
            string clientEnterFile = Path.Combine(_clientLogDir, "ClientEnterLines.txt");
            string userInputsFile = Path.Combine(_clientLogDir, "UserInputs.txt");
            string serverConsoleOutput = string.Empty;
            string clientConsoleOutput = string.Empty;
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                serverConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
                File.WriteAllText(serverLogFile, serverConsoleOutput);
            }
            if (_clientProcess != null && !_clientProcess.HasExited)
            {
                clientConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);
                string processedClientOutput = _consoleCaptureService.ProcessClientConsoleOutput(clientConsoleOutput, _enterLines, out List<string> userInputs);
                File.WriteAllText(clientLogFile, processedClientOutput);
                File.WriteAllText(clientEnterFile, string.Join(Environment.NewLine, _enterLines));
                if (userInputs.Count > 0)
                {
                    File.WriteAllText(userInputsFile, string.Join(Environment.NewLine, userInputs));
                }
            }
            try { if (_clientProcess != null && !_clientProcess.HasExited) _clientProcess.Kill(); } catch { }
            try { if (_serverProcess != null && !_serverProcess.HasExited) _serverProcess.Kill(); } catch { }
            _isSessionRunning = false;
            StartStopButton.Content = "Start Grading Session";
            var msg = new StringBuilder();
            msg.AppendLine($"Log session stopped. Data appended to:\n{_excelLogPath}");
            if (!string.IsNullOrEmpty(serverConsoleOutput)) msg.AppendLine($"Server console log saved to:\n{serverLogFile}");
            if (!string.IsNullOrEmpty(clientConsoleOutput)) msg.AppendLine($"Client console log saved to:\n{clientLogFile}");
            msg.AppendLine($"Client enter lines saved to:\n{clientEnterFile}");
            msg.AppendLine($"User inputs saved to:\n{userInputsFile}");
            MessageBox.Show(msg.ToString(), "Session Ended", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region UI Helpers
        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is LoggedRequest loggedRequest)
            {
                var msg = new StringBuilder();
                msg.AppendLine($"Timestamp: {loggedRequest.Timestamp}");
                string selectedProtocol = (ProtocolSelection.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "HTTP";
                if (selectedProtocol == "HTTP")
                {
                    msg.AppendLine($"Method: {loggedRequest.Method}");
                    msg.AppendLine($"Url: {loggedRequest.Url}");
                    msg.AppendLine($"Status Code: {loggedRequest.StatusCode}");
                    msg.AppendLine($"--- Request Body ---\n{loggedRequest.RequestBody}");
                    msg.AppendLine($"--- Response Body ---\n{loggedRequest.ResponseBody}");
                }
                else // TCP
                {
                    msg.AppendLine($"Direction: {loggedRequest.Method}");
                    msg.AppendLine($"Bytes: {loggedRequest.StatusCode}");
                    msg.AppendLine($"--- Data Content ---\n{loggedRequest.RequestBody}");
                }
                MessageBox.Show(msg.ToString(), "Captured Data", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region Additional WinAPI for Hook Filter
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        #endregion
    }
}