// MiddlewareTool/MainWindow.xaml.cs
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
using MiddlewareTool.Services;
using MiddlewareTool.Helpers;
using MiddlewareTool.Models;

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
        private List<(string Line, DateTime Timestamp)> _enterLines = new List<(string Line, DateTime Timestamp)>();
        private List<(int Stage, DateTime Timestamp, string ClientOutput, string ServerOutput)> _stageCaptures = new List<(int Stage, DateTime Timestamp, string ClientOutput, string ServerOutput)>();
        private string _clientLogDir = "";

        // === THÊM BIẾN NÀY ===
        private List<string> _currentPrompts = new List<string>();

        private ProxyService _proxyService;
        private ExcelLogger _excelLogger;
        private ConsoleCaptureService _consoleCaptureService;
        private AppSettingsReplacer _appSettingsReplacer;

        public MainWindow()
        {
            InitializeComponent();
            LoggedRequests = new ObservableCollection<LoggedRequest>();
            RequestsGrid.ItemsSource = LoggedRequests;
            _excelLogger = new ExcelLogger();
            _proxyService = new ProxyService(PROXY_PORT, REAL_SERVER_PORT, LoggedRequests, _excelLogger);
            _consoleCaptureService = new ConsoleCaptureService();
            _appSettingsReplacer = new AppSettingsReplacer();
        }

        #region File Dialog Handlers
        private void BrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Server Executable"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ServerExePath.Text = openFileDialog.FileName;
            }
        }

        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Client Executable"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ClientExePath.Text = openFileDialog.FileName;
            }
        }

        private void BrowseAppSetting_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select appsettings.json template for server"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                AppSettingTemplate.Text = openFileDialog.FileName;
            }
        }

        private void BrowseAppSettingClient_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select appsettings.json template for client"
            };
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

        // === THÊM HÀM MỚI NÀY ===
        private void BrowsePrompts_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Select Prompts file"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                PromptFilePath.Text = openFileDialog.FileName;
            }
        }
        // === KẾT THÚC PHẦN THÊM ===

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
                // === SỬA LẠI HÀM IF NÀY ===
                if (string.IsNullOrEmpty(ServerExePath.Text) ||
                    string.IsNullOrEmpty(ClientExePath.Text) ||
                    string.IsNullOrEmpty(AppSettingTemplate.Text) ||
                    string.IsNullOrEmpty(AppSettingClientTemplate.Text) ||
                    string.IsNullOrEmpty(_excelLogPath) ||
                    string.IsNullOrEmpty(PromptFilePath.Text)) // <-- Thêm kiểm tra
                {
                    // Sửa lại thông báo lỗi
                    MessageBox.Show("Please provide all required paths, including the log file AND the prompts file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // === KẾT THÚC SỬA ĐỔI ===
                await StartSessionAsync();
            }
        }

        private async Task StartSessionAsync()
        {
            _appSettingsReplacer.ReplaceSetting(ServerExePath.Text, AppSettingTemplate.Text);
            _appSettingsReplacer.ReplaceSetting(ClientExePath.Text, AppSettingClientTemplate.Text);
            StartStopButton.Content = "Stop Grading Session";
            _isSessionRunning = true;
            LoggedRequests.Clear();
            _excelLogger.SetupExcelLogFile(_excelLogPath);

            _serverProcess = new Process { StartInfo = new ProcessStartInfo(ServerExePath.Text, REAL_SERVER_PORT.ToString()) { UseShellExecute = true } };
            _serverProcess.Start();

            _cts = new CancellationTokenSource();
            string selectedProtocol = (ProtocolSelection.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "HTTP";
            _proxyService.StartProxy(selectedProtocol, _cts.Token);

            // === THÊM LOGIC ĐỌC FILE PROMPTS ===
            _currentPrompts.Clear(); // Xoá prompts cũ
            try
            {
                _currentPrompts = File.ReadAllLines(PromptFilePath.Text)
                                     .Where(line => !string.IsNullOrWhiteSpace(line)) // Bỏ qua dòng trống
                                     .Select(line => line.Trim()) // Xoá khoảng trắng thừa
                                     .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read prompts file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopSession(); // Dừng lại nếu không đọc được file
                return;
            }
            if (_currentPrompts.Count == 0)
            {
                MessageBox.Show($"Prompts file is empty. Please add prompts to the file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopSession();
                return;
            }
            // === KẾT THÚC PHẦN THÊM ===

            await Task.Delay(1500);
            _clientProcess = new Process { StartInfo = new ProcessStartInfo(ClientExePath.Text) { UseShellExecute = true } };
            _clientProcess.Start();
            await Task.Delay(500); // Wait for client window to open
            _clientLogDir = Path.GetDirectoryName(_excelLogPath) ?? "";
            _enterLines.Clear();
            _stageCaptures.Clear();
            KeyboardHook.SetHook(OnEnterPressed);
        }

        // === THAY THẾ TOÀN BỘ HÀM OnEnterPressed ===
        private async void OnEnterPressed()
        {
            if (_clientProcess == null || _clientProcess.HasExited) return;
            IntPtr foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid != (uint)_clientProcess.Id) return; // Not client window

            string clientOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);
            if (string.IsNullOrEmpty(clientOutput)) return;

            // Logic mới: Chỉ cần lấy dòng cuối cùng không trống
            string lastLine = clientOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                        .LastOrDefault(line => !string.IsNullOrWhiteSpace(line));

            string fullTrimmed = lastLine?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(fullTrimmed) && !_enterLines.Any(e => e.Line == fullTrimmed))
            {
                DateTime now = DateTime.Now;
                _enterLines.Add((fullTrimmed, now));

                // Delay to allow response to be printed
                await Task.Delay(500); // 500ms delay to wait for response output

                // Capture client and server at this stage
                int stage = _enterLines.Count;
                string serverOutput = string.Empty;
                string delayedClientOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    serverOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
                }
                _stageCaptures.Add((stage, now, delayedClientOutput, serverOutput));
            }
        }
        // === KẾT THÚC THAY THẾ ===

        private async void StopSession()
        {
            KeyboardHook.Unhook();
            _cts?.Cancel();
            _proxyService.StopProxy();
            string clientLogFile = Path.Combine(_clientLogDir, $"{Path.GetFileNameWithoutExtension(_excelLogPath)}_Client.log");
            string serverLogFile = Path.Combine(_clientLogDir, $"{Path.GetFileNameWithoutExtension(_excelLogPath)}_Server.log");
            string clientEnterFile = Path.Combine(_clientLogDir, $"{Path.GetFileNameWithoutExtension(_excelLogPath)}_EnterLines.log");
            string userInputsFile = Path.Combine(_clientLogDir, $"{Path.GetFileNameWithoutExtension(_excelLogPath)}_UserInputs.log");
            string serverConsoleOutput = string.Empty;
            string clientConsoleOutput = string.Empty;

            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                serverConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
                File.WriteAllText(serverLogFile, serverConsoleOutput);
            }

            List<(int Stage, string Input, string Timestamp)> stageInputs;

            if (_clientProcess != null && !_clientProcess.HasExited)
            {
                await Task.Delay(500); // Delay for final outputs
                clientConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);

                // Add final stage for stop
                DateTime stopTime = DateTime.Now;
                int finalStage = _stageCaptures.Count + 1;
                serverConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess?.Id ?? 0);
                _stageCaptures.Add((finalStage, stopTime, clientConsoleOutput, serverConsoleOutput));

                // === SỬA LẠI CÁCH GỌI HÀM NÀY ===
                string processedClientOutput = _consoleCaptureService.ProcessClientConsoleOutput(
                    clientConsoleOutput,
                    _enterLines,
                    _currentPrompts,  // <-- Truyền danh sách prompts đã đọc
                    out stageInputs
                );
                // === KẾT THÚC SỬA ĐỔI ===

                File.WriteAllText(clientLogFile, processedClientOutput);
                File.WriteAllText(clientEnterFile, string.Join(Environment.NewLine, _enterLines.Select(e => e.Line)));
                if (stageInputs.Count > 0)
                {
                    File.WriteAllText(userInputsFile, string.Join(Environment.NewLine, stageInputs.Select(s => $"Stage {s.Stage}: {s.Input} at {s.Timestamp}")));
                    _excelLogger.AppendStagesToExcel(stageInputs);
                }
                if (_stageCaptures.Count > 0)
                {
                    _excelLogger.AppendClientStagesToExcel(_stageCaptures);
                    _excelLogger.AssignStagesToLogs(_stageCaptures.Select(s => (s.Stage, s.Timestamp)).ToList());
                }
            }

            try { if (_clientProcess != null && !_clientProcess.HasExited) _clientProcess.Kill(); } catch { }
            try { if (_serverProcess != null && !_serverProcess.HasExited) _serverProcess.Kill(); } catch { }
            _isSessionRunning = false;
            StartStopButton.Content = "Start Grading Session";

            MessageBox.Show($"Session stopped. Logs saved to {_excelLogPath} and related log files in {_clientLogDir}", "Session Stopped", MessageBoxButton.OK, MessageBoxImage.Information);
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