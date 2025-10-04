using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;

using System.Windows;

using UITestKit.Model;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Text;

namespace UITestKit
{
    /// <summary>
    /// Interaction logic for MiddlwareTool.xaml
    /// </summary>
    public partial class MiddlwareTool : Window
    {
        private static readonly string TARGET_FILE = "appsettings.json";
        private const int PROXY_PORT = 5000;
        private const int REAL_SERVER_PORT = 5001;

        // HTTP specific
        private HttpListener? _httpListener;

        // TCP specific (NEW)
        private TcpListener? _tcpListener;

        private Process? _serverProcess;
        private Process? _clientProcess;
        private CancellationTokenSource? _cts;
        private bool _isSessionRunning = false;

        public ObservableCollection<LoggedRequest> LoggedRequests { get; set; }
        public MiddlwareTool()
        {
            InitializeComponent();
            LoggedRequests = new ObservableCollection<LoggedRequest>();
            RequestsGrid.ItemsSource = LoggedRequests;
        }
        //======================= Lấy đường dẫn file Server
        //================================================ 
        private void BrowseServer_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe" };
            if (openFileDialog.ShowDialog() == true)
            {
                ServerExePath.Text = openFileDialog.FileName;
            }
        }

        //======================= Lấy đường dẫn file Client
        //================================================
        private void BrowseClient_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe" };
            if (openFileDialog.ShowDialog() == true)
            {
                ClientExePath.Text = openFileDialog.FileName;
            }
        }

        //======================= Lấy đường dẫn file appsettings.json Server
        //================================================
        private void AppsettingTemplate_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                AppSettingTemplate.Text = openFileDialog.FileName;
            }
        }

        //======================= Lấy đường dẫn file appsettings.json Client
        //================================================
        private void AppsettingClientTemplate_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                AppSettingClientTemplate.Text = openFileDialog.FileName;
            }
        }

        //======================= Bắt đầu hoặc dừng phiên
        //================================================

        private async void StartStop_Click(object sender, RoutedEventArgs e)//done
        {
            if (_isSessionRunning)
            {
                StopSession();
            }
            else
            {
                if (string.IsNullOrEmpty(ServerExePath.Text) || string.IsNullOrEmpty(ClientExePath.Text)
                    || string.IsNullOrEmpty(AppSettingTemplate.Text) || string.IsNullOrEmpty(AppSettingClientTemplate.Text))
                {
                    MessageBox.Show("Please input all fields.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                await StartSessionAsync();
            }
        }

        private async Task StartSessionAsync()//done
        {
            ReplaceAppSetting();
            StartStopButton.Content = "Stop Grading Session";
            _isSessionRunning = true;
            LoggedRequests.Clear();

            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo(ServerExePath.Text, REAL_SERVER_PORT.ToString()) { UseShellExecute = true }
            };
            _serverProcess.Start();

            _cts = new CancellationTokenSource();

            // MODIFIED: Check protocol type
            string selectedProtocol = (ProtocolSelection.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "HTTP";

            if (selectedProtocol == "HTTP")
            {
                StartHttpProxy(_cts.Token);
            }
            else // TCP
            {
                StartTcpProxy(_cts.Token);
            }

            await Task.Delay(1500); // Wait a bit for server and proxy to be ready

            _clientProcess = new Process
            {
                StartInfo = new ProcessStartInfo(ClientExePath.Text) { UseShellExecute = true }
            };
            _clientProcess.Start();
        }

        private void StopSession()//done
        {
            _cts?.Cancel();
            _httpListener?.Stop();
            _tcpListener?.Stop(); // NEW

            try { if (_clientProcess != null && !_clientProcess.HasExited) _clientProcess.Kill(); } catch { }
            try { if (_serverProcess != null && !_serverProcess.HasExited) _serverProcess.Kill(); } catch { }

            _isSessionRunning = false;
            StartStopButton.Content = "Start Grading Session";
        }
        //=============================== Kết thúc bắt đầu hoặc dừng phiên
        //===============================================================


        //========================================== Xử lí Http Proxy
        //===============================================================
        private void StartHttpProxy(CancellationToken token)//done
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{PROXY_PORT}/");
            _httpListener.Start();
            Task.Run(() => ListenForHttpRequests(token), token);
        }

        private async Task ListenForHttpRequests(CancellationToken token)//done
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => ProcessHttpRequest(context), token);
                }
                catch (HttpListenerException) { break; } // Listener stopped
            }
        }

        private async Task ProcessHttpRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var logEntry = new LoggedRequest { Method = request.HttpMethod, Url = request.Url.ToString() };

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
                    if (request.ContentType != null) { contentType = MediaTypeHeaderValue.Parse(request.ContentType); }
                    forwardRequest.Content = new StringContent(logEntry.RequestBody, contentType);

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
            catch (Exception ex) { Console.WriteLine($"HTTP Forward Error: {ex.Message}"); }

            Application.Current.Dispatcher.Invoke(() => LoggedRequests.Add(logEntry));
        }
        //============================================= kết thúc xử lí HTTP Proxy
        //===========================================================================


        //============================================= Xử lí TCP Proxy 
        //===========================================================================
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
                catch (OperationCanceledException) { break; } // Listener stopped
                catch (Exception ex) { Console.WriteLine($"TCP Accept Error: {ex.Message}"); }
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
            catch (Exception ex) { Console.WriteLine($"TCP Connection Error: {ex.Message}"); }
        }

        private async Task RelayDataAsync(NetworkStream fromStream, NetworkStream toStream, string direction, CancellationToken token)
        {
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await fromStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await toStream.WriteAsync(buffer, 0, bytesRead, token);

                // Log the data packet
                string dataPreview = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var logEntry = new LoggedRequest
                {
                    Method = direction,
                    Url = dataPreview.Length > 100 ? dataPreview.Substring(0, 100) + "..." : dataPreview,
                    RequestBody = dataPreview, // Store full data here for view button
                    StatusCode = bytesRead
                };
                Application.Current.Dispatcher.Invoke(() => LoggedRequests.Add(logEntry));
            }
        }
        //============================================= kết thúc xử lí TCP Proxy
        //===========================================================================

        //====================== Xử lí nút View 
        //===========================================================================
        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is LoggedRequest loggedRequest)
            {
                StringBuilder msg = new StringBuilder();
                msg.AppendLine($"Timestamp: {loggedRequest.Timestamp}");

                // Adapt message for both protocols
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


        //====================== Thay thế file appsettings.json
        //===========================================================================
        private void ReplaceAppSetting()//done
        {
            string serverTemplatePath = AppSettingTemplate.Text;
            string clientTemplatePath = AppSettingClientTemplate.Text;
            string serverDestDir = Path.GetDirectoryName(ServerExePath.Text);
            string clientDestDir = Path.GetDirectoryName(ClientExePath.Text);

            Action<string, string> copyFile = (template, destDir) =>
            {
                if (!File.Exists(template) || !Directory.Exists(destDir)) return;
                string[] searchResults = Directory.GetFiles(destDir, TARGET_FILE, SearchOption.AllDirectories);
                foreach (string item in searchResults)
                {
                    try { File.Copy(template, item, true); }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
            };

            copyFile(serverTemplatePath, serverDestDir);
            copyFile(clientTemplatePath, clientDestDir);
        }

    }
}