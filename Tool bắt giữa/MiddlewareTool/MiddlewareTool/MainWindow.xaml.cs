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
        private List<(int Stage, string Line, DateTime Timestamp)> _enterLines = new List<(int Stage, string Line, DateTime Timestamp)>();
        private List<(int Stage, DateTime Timestamp, string ClientOutput, string ServerOutput)> _stageCaptures = new List<(int Stage, DateTime Timestamp, string ClientOutput, string ServerOutput)>();
        private string _clientLogDir = "";

        // Baseline captures for each stage
        private List<(int Stage, DateTime Timestamp, string Baseline)> _baselineCaptures = new List<(int Stage, DateTime Timestamp, string Baseline)>();
        private int _currentStage = 0;

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
                AppSettingServerTemplate.Text = openFileDialog.FileName;
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
                    string.IsNullOrEmpty(AppSettingServerTemplate.Text) ||
                    string.IsNullOrEmpty(AppSettingClientTemplate.Text) ||
                    string.IsNullOrEmpty(_excelLogPath))
                {
                    MessageBox.Show("Please provide all required paths.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                await StartSessionAsync();
            }
        }

        private async Task StartSessionAsync()
        {
            _appSettingsReplacer.ReplaceSetting(ServerExePath.Text, AppSettingServerTemplate.Text);
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

            await Task.Delay(1500);
            _clientProcess = new Process { StartInfo = new ProcessStartInfo(ClientExePath.Text) { UseShellExecute = true } };
            _clientProcess.Start();
            await Task.Delay(500); // Wait for client window to open
            _clientLogDir = Path.GetDirectoryName(_excelLogPath) ?? "";
            _enterLines.Clear();
            _stageCaptures.Clear();
            _baselineCaptures.Clear();
            _currentStage = 0;
            KeyboardHook.SetHook(OnEnterPressed, OnCapturePressed);
            
            StatusText.Text = "Status: Session running. Press F5 or Enter in client console to capture Stage 1.";
            StatusText.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }

        private void OnCapturePressed()
        {
            if (_clientProcess == null || _clientProcess.HasExited) return;
            IntPtr foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid != (uint)_clientProcess.Id) return; // Not client window

            string clientOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);
            if (string.IsNullOrEmpty(clientOutput)) return;

            // Each F5 press creates a new stage
            _currentStage++;
            DateTime now = DateTime.Now;
            
            // Capture client and server output for this stage
            string serverOutput = string.Empty;
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                serverOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
            }
            
            _stageCaptures.Add((_currentStage, now, clientOutput, serverOutput));

            // Update status text
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Status: Stage {_currentStage} captured (F5). Press F5 or Enter for next stage.";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
            });
        }

        private async void OnEnterPressed()
        {
            if (_clientProcess == null || _clientProcess.HasExited) return;
            IntPtr foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid != (uint)_clientProcess.Id) return; // Not client window

            string clientOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);
            if (string.IsNullOrEmpty(clientOutput)) return;

            // Each Enter press creates a new stage
            _currentStage++;
            DateTime now = DateTime.Now;

            // Capture client and server output for this stage
            string serverOutput = string.Empty;
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                serverOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
            }
            
            _stageCaptures.Add((_currentStage, now, clientOutput, serverOutput));

            // Update status
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Status: Stage {_currentStage} captured (Enter). Press F5 or Enter for next stage.";
                StatusText.Foreground = System.Windows.Media.Brushes.Blue;
            });
        }

        private string ExtractInputFromBaseline(string baseline, string currentOutput)
        {
            // Split both outputs into lines
            var baselineLines = baseline.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var currentLines = currentOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            // Find the last non-empty line in baseline - this should be the prompt
            string lastBaselineLine = baselineLines.LastOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(lastBaselineLine))
            {
                return string.Empty;
            }

            // Search from the END of current output to find the LAST matching line
            // This handles cases where the same prompt appears multiple times in the console
            for (int i = currentLines.Length - 1; i >= 0; i--)
            {
                string trimmedLine = currentLines[i].Trim();
                
                // Check if this line starts with the baseline prompt
                if (trimmedLine.StartsWith(lastBaselineLine))
                {
                    if (trimmedLine.Length > lastBaselineLine.Length)
                    {
                        // Found the line with user input appended to prompt
                        return trimmedLine.Substring(lastBaselineLine.Length).Trim();
                    }
                    // If we found a matching line but no input yet, this might be the prompt line
                    // Check if there's content after this line (for multi-line input scenarios)
                    break;
                }
            }

            // Fallback: Compare the two outputs and find what's new
            // Find where baseline ends and new content begins
            int matchingLines = 0;
            for (int i = 0; i < Math.Min(baselineLines.Length, currentLines.Length); i++)
            {
                if (baselineLines[i] == currentLines[i])
                {
                    matchingLines = i + 1;
                }
                else
                {
                    break;
                }
            }

            // Get new content after the matching part
            if (matchingLines < currentLines.Length)
            {
                var newLines = currentLines.Skip(matchingLines)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToList();
                
                if (newLines.Any())
                {
                    // Look for input on the same line as prompt or on new lines
                    string firstNewLine = newLines.First();
                    if (firstNewLine.Contains(lastBaselineLine))
                    {
                        // Input is on the same line as prompt
                        int promptIndex = firstNewLine.IndexOf(lastBaselineLine);
                        if (promptIndex >= 0)
                        {
                            string afterPrompt = firstNewLine.Substring(promptIndex + lastBaselineLine.Length).Trim();
                            if (!string.IsNullOrEmpty(afterPrompt))
                            {
                                return afterPrompt;
                            }
                        }
                    }
                    // Return all new content
                    return string.Join(" ", newLines);
                }
            }

            return string.Empty;
        }

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
            List<(int Stage, string Input, string Timestamp)> stageInputs;

            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                serverConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
                File.WriteAllText(serverLogFile, serverConsoleOutput);
            }

            if (_clientProcess != null && !_clientProcess.HasExited)
            {
                await Task.Delay(500); // Delay for final outputs
                clientConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_clientProcess.Id);

                // Add final stage for stop to capture complete console at end
                _currentStage++;
                DateTime stopTime = DateTime.Now;
                serverConsoleOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess?.Id ?? 0);
                _stageCaptures.Add((_currentStage, stopTime, clientConsoleOutput, serverConsoleOutput));

                // Build stage inputs from _enterLines
                stageInputs = new List<(int Stage, string Input, string Timestamp)>();
                for (int i = 0; i < _enterLines.Count; i++)
                {
                    stageInputs.Add((_enterLines[i].Stage, _enterLines[i].Line, _enterLines[i].Timestamp.ToString("HH:mm:ss.fff")));
                }

                File.WriteAllText(clientLogFile, clientConsoleOutput);
                File.WriteAllText(clientEnterFile, string.Join(Environment.NewLine, _enterLines.Select(e => $"Stage {e.Stage}: {e.Line}")));
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
            else
            {
                stageInputs = new List<(int Stage, string Input, string Timestamp)>();
            }

            try { if (_clientProcess != null && !_clientProcess.HasExited) _clientProcess.Kill(); } catch { }
            try { if (_serverProcess != null && !_serverProcess.HasExited) _serverProcess.Kill(); } catch { }
            _isSessionRunning = false;
            StartStopButton.Content = "Start Grading Session";
            
            StatusText.Text = $"Status: Session stopped. {_currentStage} stages captured.";
            StatusText.Foreground = System.Windows.Media.Brushes.Gray;

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