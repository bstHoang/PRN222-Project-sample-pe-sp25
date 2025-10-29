using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MiddlewareTool
{
    public partial class MainWindow : Window
    {
        private static readonly string TARGET_FILE = "appsettings.json";
        private const int PROXY_PORT = 5000;
        private const int REAL_SERVER_PORT = 5001;
        private string _excelLogPath = "";
        private static readonly object _excelLock = new object();
        private HttpListener? _httpListener;
        private TcpListener? _tcpListener;
        private Process? _serverProcess;
        private Process? _clientProcess;
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _proxyCts; // Cần cho các task relay console
        private bool _isSessionRunning = false;

        // StringBuilders để ghi log input/output của client
        private readonly StringBuilder _clientOutputLog = new StringBuilder();
        private readonly StringBuilder _clientInputLog = new StringBuilder();

        public ObservableCollection<LoggedRequest> LoggedRequests{get; set; }
        public MainWindow()
        {
            InitializeComponent();
            LoggedRequests = new ObservableCollection<LoggedRequest>();
            RequestsGrid.ItemsSource = LoggedRequests;
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
            ReplaceAppSetting();
            StartStopButton.Content = "Stop Grading Session";
            _isSessionRunning = true;
            LoggedRequests.Clear();
            SetupExcelLogFile(_excelLogPath);

            // 1. Khởi động Server (như cũ)
            _serverProcess = new Process { StartInfo = new ProcessStartInfo(ServerExePath.Text, REAL_SERVER_PORT.ToString()) { UseShellExecute = true } };
            _serverProcess.Start();

            // 2. Khởi động Proxy HTTP/TCP (như cũ)
            _cts = new CancellationTokenSource();
            string selectedProtocol = (ProtocolSelection.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "HTTP";

            if (selectedProtocol == "HTTP")
            {
                StartHttpProxy(_cts.Token);
            }
            else // TCP
            {
                StartTcpProxy(_cts.Token);
            }

            // Đợi server/proxy sẵn sàng
            await Task.Delay(1500);

            // 3. *** BẮT ĐẦU CLIENT PROXY (Logic mới) ***
            _clientOutputLog.Clear();
            _clientInputLog.Clear();
            _proxyCts = new CancellationTokenSource();

            // Mở một console "giả" MỚI để người dùng tương tác
            if (!AllocConsole())
            {
                MessageBox.Show("Failed to allocate console for client.");
                StopSession(); // Dừng lại nếu không tạo được console
                return;
            }

            _clientProcess = new Process
            {
                StartInfo = new ProcessStartInfo(ClientExePath.Text)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true, // Chạy client.exe ở chế độ ẩn
                    StandardOutputEncoding = Encoding.UTF8, // Đảm bảo encoding
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = Path.GetDirectoryName(ClientExePath.Text) ?? "" // Rất quan trọng
                },
                EnableRaisingEvents = true
            };

            // Bắt đầu 3 task relay
            _clientProcess.Start();

            _ = Task.Run(() => RelayStreamAsync(_clientProcess.StandardOutput, Console.Out, _clientOutputLog, _proxyCts.Token), _proxyCts.Token);
            _ = Task.Run(() => RelayStreamAsync(_clientProcess.StandardError, Console.Error, _clientOutputLog, _proxyCts.Token), _proxyCts.Token); // Log cả Error vào Output
            _ = Task.Run(() => RelayInputAsync(Console.In, _clientProcess.StandardInput, _clientInputLog, _proxyCts.Token), _proxyCts.Token);

            // Task theo dõi process client: nếu client tự tắt, StopSession
            _ = _clientProcess.WaitForExitAsync(_proxyCts.Token).ContinueWith(t =>
            {
                // Phải dùng Dispatcher vì nó sẽ update UI (nút Stop)
                Application.Current.Dispatcher.Invoke(() => StopSession());
            }, TaskContinuationOptions.NotOnCanceled);
        }

        private void StopSession()
        {
            // Kiểm tra, tránh gọi lại (vì client exit cũng trigger StopSession)
            if (!_isSessionRunning) return;
            _isSessionRunning = false; // Đặt cờ này ngay lập tức

            _cts?.Cancel();         // Stop proxy HTTP/TCP
            _proxyCts?.Cancel();    // Stop proxy Console
            _httpListener?.Stop();
            _tcpListener?.Stop();

            // Đóng console "giả"
            FreeConsole();

            // --- Định nghĩa đường dẫn file ---
            string basePath = Path.GetDirectoryName(_excelLogPath) ?? "";
            string serverLogFile = Path.Combine(basePath, "ServerConsoleLog.txt");
            string clientOutputLogFile = Path.Combine(basePath, "ClientConsoleLog.txt");
            string clientInputLogFile = Path.Combine(basePath, "ClientInputLog.txt");

            string serverConsoleOutput = string.Empty;

            // --- Chụp Server (vẫn dùng P/Invoke) ---
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                serverConsoleOutput = CaptureConsoleOutput(_serverProcess.Id);
                File.WriteAllText(serverLogFile, serverConsoleOutput);
            }

            // --- Kill processes ---
            try { if (_clientProcess != null && !_clientProcess.HasExited) _clientProcess.Kill(); } catch { }
            try { if (_serverProcess != null && !_serverProcess.HasExited) _serverProcess.Kill(); } catch { }

            // --- Dispose ---
            _clientProcess?.Dispose();
            _serverProcess?.Dispose();
            _proxyCts?.Dispose();
            _cts?.Dispose();

            // --- Ghi log Client từ StringBuilders ---
            File.WriteAllText(clientOutputLogFile, _clientOutputLog.ToString());
            File.WriteAllText(clientInputLogFile, _clientInputLog.ToString());

            // --- Thông báo ---
            StartStopButton.Content = "Start Grading Session";

            var msg = new StringBuilder();
            msg.AppendLine($"Log session stopped. Data appended to:\n{_excelLogPath}");
            if (!string.IsNullOrEmpty(serverConsoleOutput)) msg.AppendLine($"Server console log saved to:\n{serverLogFile}");
            if (File.Exists(clientOutputLogFile)) msg.AppendLine($"Client OUTPUT log saved to:\n{clientOutputLogFile}");
            if (File.Exists(clientInputLogFile)) msg.AppendLine($"Client INPUT log saved to:\n{clientInputLogFile}");

            MessageBox.Show(msg.ToString(), "Session Ended", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Đọc từ client (Output/Error) và ghi ra Console MỚI + Log
        private async Task RelayStreamAsync(StreamReader fromStream, TextWriter toWriter, StringBuilder logger, CancellationToken token)
        {
            char[] buffer = new char[1024];
            int bytesRead;
            try
            {
                while (!token.IsCancellationRequested && (bytesRead = await fromStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string text = new string(buffer, 0, bytesRead);
                    await toWriter.WriteAsync(text); // Ghi ra console của chúng ta
                    logger.Append(text);             // Ghi vào log
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Có thể xảy ra lỗi khi stop, không cần báo
            }
        }

        // Đọc từ Console MỚI (Input) và ghi vào client + Log
        private async Task RelayInputAsync(TextReader fromReader, StreamWriter toWriter, StringBuilder logger, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Phải dùng Task.Run để chạy ReadLine trong thread khác
                    // vì nó là blocking call và ReadLineAsync trên Console.In không hoạt động như mong đợi
                    string? line = await Task.Run(() => fromReader.ReadLine(), token);

                    if (line != null)
                    {
                        await toWriter.WriteLineAsync(line); // Ghi vào client.exe
                        logger.AppendLine(line);             // Ghi vào log input
                    }
                    else
                    {
                        break; // End of stream
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Có thể xảy ra lỗi khi stop, không cần báo
            }
        }

        #endregion

        #region Proxy Logic
        private void StartHttpProxy(CancellationToken token)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{PROXY_PORT}/");
            _httpListener.Start();
            Task.Run(() => ListenForHttpRequests(token), token);
        }
        private async Task ListenForHttpRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => ProcessHttpRequest(context), token);
                }
                catch (HttpListenerException)
                {
                    break;
                }
            }
        }
        private async Task ProcessHttpRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var logEntry = new LoggedRequest
            {
                Method = request.HttpMethod,
                Url = request.Url.ToString()
            };
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                logEntry.RequestBody = await reader.ReadToEndAsync();
            }
            try
            {
                var realServerUrl = $"http://localhost:{REAL_SERVER_PORT}{request.Url.AbsolutePath}";
                using (var client = new HttpClient())
                {
                    var forwardRequest = new HttpRequestMessage(new HttpMethod(request.HttpMethod), realServerUrl);
                    MediaTypeHeaderValue? contentType = null;
                    if (request.ContentType != null)
                    {
                        contentType = MediaTypeHeaderValue.Parse(request.ContentType);
                    }

                    // Sửa lỗi biên dịch tiềm năng từ code gốc
                    Encoding encoding = request.ContentEncoding ?? Encoding.UTF8;
                    forwardRequest.Content = new StringContent(logEntry.RequestBody, encoding, contentType?.MediaType);


                    var responseMessage = await client.SendAsync(forwardRequest);
                    var response = context.Response;
                    logEntry.StatusCode = (int)responseMessage.StatusCode;
                    var responseContent = await responseMessage.Content.ReadAsByteArrayAsync();
                    logEntry.ResponseBody = Encoding.UTF8.GetString(responseContent);
                    response.StatusCode = (int)responseMessage.StatusCode;
                    response.ContentLength64 = responseContent.Length;
                    response.ContentType = responseMessage.Content.Headers.ContentType?.ToString();
                    await response.OutputStream.WriteAsync(responseContent, 0, responseContent.Length);
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP Forward Error: {ex.Message}");
            }
            AppendToExcelLog(logEntry);
            Application.Current.Dispatcher.Invoke(() => LoggedRequests.Add(logEntry));
        }
        private void StartTcpProxy(CancellationToken token)
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, PROXY_PORT);
            _tcpListener.Start();
            Task.Run(() => ListenForTcpConnections(token), token);
        }
        private async Task ListenForTcpConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var clientConnection = await _tcpListener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => ProcessTcpConnection(clientConnection, token), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"TCP Accept Error: {ex.Message}");
                }
            }
        }
        private async Task ProcessTcpConnection(TcpClient clientConnection, CancellationToken token)
        {
            try
            {
                using (clientConnection)
                using (var serverConnection = new TcpClient())
                {
                    await serverConnection.ConnectAsync(IPAddress.Loopback, REAL_SERVER_PORT, token);
                    using (var clientStream = clientConnection.GetStream())
                    using (var serverStream = serverConnection.GetStream())
                    {
                        var clientToServer = RelayDataAsync(clientStream, serverStream, "Client -> Server", token);
                        var serverToClient = RelayDataAsync(serverStream, clientStream, "Server -> Client", token);
                        await Task.WhenAny(clientToServer, serverToClient);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TCP Connection Error: {ex.Message}");
            }
        }
        private async Task RelayDataAsync(NetworkStream fromStream, NetworkStream toStream, string direction, CancellationToken token)
        {
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await fromStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await toStream.WriteAsync(buffer, 0, bytesRead, token);
                string dataPreview = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var logEntry = new LoggedRequest
                {
                    Method = direction,
                    Url = dataPreview.Length > 100 ? dataPreview.Substring(0, 100) + "..." : dataPreview,
                    RequestBody = dataPreview,
                    StatusCode = bytesRead
                };
                AppendToExcelLog(logEntry);
                Application.Current.Dispatcher.Invoke(() => LoggedRequests.Add(logEntry));
            }
        }
        #endregion

        #region Excel Handling
        private void SetupExcelLogFile(string path)
        {
            lock (_excelLock)
            {
                if (!File.Exists(path))
                {
                    string directory = Path.GetDirectoryName(path);
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Logs");
                        worksheet.Cell(1, 1).Value = "Timestamp";
                        worksheet.Cell(1, 2).Value = "Method/Direction";
                        worksheet.Cell(1, 3).Value = "URL/Data Preview";
                        worksheet.Cell(1, 4).Value = "Status/Bytes";
                        worksheet.Cell(1, 5).Value = "Request Body / Data";
                        worksheet.Cell(1, 6).Value = "Response Body";
                        worksheet.Row(1).Style.Font.Bold = true;
                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(path);
                    }
                }
            }
        }
        private void AppendToExcelLog(LoggedRequest logEntry)
        {
            if (string.IsNullOrEmpty(_excelLogPath)) return;
            lock (_excelLock)
            {
                try
                {
                    using (var workbook = new XLWorkbook(_excelLogPath))
                    {
                        var worksheet = workbook.Worksheet(1);
                        int newRow = worksheet.LastRowUsed().RowNumber() + 1;
                        worksheet.Cell(newRow, 1).Value = logEntry.Timestamp;
                        worksheet.Cell(newRow, 2).Value = logEntry.Method;
                        worksheet.Cell(newRow, 3).Value = logEntry.Url;
                        worksheet.Cell(newRow, 4).Value = logEntry.StatusCode;
                        worksheet.Cell(newRow, 5).Value = logEntry.RequestBody;
                        worksheet.Cell(newRow, 6).Value = logEntry.ResponseBody;
                        workbook.Save();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to Excel log: {ex.Message}");
                }
            }
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
        private void ReplaceAppSetting()
        {
            string serverTemplatePath = AppSettingTemplate.Text;
            string clientTemplatePath = AppSettingClientTemplate.Text;
            string serverDestDir = Path.GetDirectoryName(ServerExePath.Text);
            string clientDestDir = Path.GetDirectoryName(ClientExePath.Text);
            Action<string, string> copyFile = (template, destDir) =>
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
            };
            copyFile(serverTemplatePath, serverDestDir);
            copyFile(clientTemplatePath, clientDestDir);
        }
        #endregion

        #region Console Capture

        // P/Invoke để TẠO/ĐÓNG console "giả"
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeConsole();


        // P/Invoke để "chụp ảnh" console (vẫn cần cho Server.exe)
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

        // Cần DllImport("kernel32.dll") cho FreeConsole, nhưng đã khai báo ở trên rồi
        // private static extern bool FreeConsole(); 

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ReadConsoleOutputCharacter(IntPtr hConsoleOutput, [Out] StringBuilder lpCharacter, uint nLength, COORD dwReadCoord, out uint lpNumberOfCharsRead);

        private string CaptureConsoleOutput(int processId)
        {
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

                // Sửa lỗi code gốc (thiếu kiểu generic)
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
                // Phải FreeConsole khỏi process server
                FreeConsole();
            }
        }
        #endregion
    }
}