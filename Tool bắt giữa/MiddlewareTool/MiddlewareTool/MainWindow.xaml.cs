using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace MiddlewareTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string TARGET_FILE = "appsettings.json";

        private const string PROXY_PORT = "5000";
        private const string REAL_SERVER_PORT = "5001";

        private HttpListener? _proxyListener;
        private Process? _serverProcess;
        private Process? _clientProcess;
        private CancellationTokenSource? _cts;
        private bool _isSessionRunning = false;

        public ObservableCollection<LoggedRequest> LoggedRequests { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            LoggedRequests = new ObservableCollection<LoggedRequest>();
            RequestsGrid.ItemsSource = LoggedRequests;
        }

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
            var openFileDialog = new OpenFileDialog
                { Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                AppSettingTemplate.Text = openFileDialog.FileName;
            }
        }

        private async void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_isSessionRunning)
            {
                StopSession();
            }
            else
            {
                if (string.IsNullOrEmpty(ServerExePath.Text) || string.IsNullOrEmpty(ClientExePath.Text) ||
                    string.IsNullOrEmpty(AppSettingTemplate.Text))
                {
                    MessageBox.Show("Please input all fields.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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

            // 1. Khởi chạy server của sinh viên trên cổng PHỤ
            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo(ServerExePath.Text, REAL_SERVER_PORT)
                {
                    UseShellExecute = true, // Mở trong cửa sổ console riêng
                }
            };
            _serverProcess.Start();

            // 2. Khởi chạy Proxy Listener trên cổng CHÍNH
            _cts = new CancellationTokenSource();
            _proxyListener = new HttpListener();
            _proxyListener.Prefixes.Add($"http://localhost:{PROXY_PORT}/");
            _proxyListener.Start();

            // Chạy vòng lặp lắng nghe trên một thread riêng
            _ = Task.Run(() => ListenForRequests(_cts.Token));

            // Chờ một chút để server và proxy sẵn sàng
            await Task.Delay(1000);

            // 3. Khởi chạy client của sinh viên
            _clientProcess = new Process
            {
                StartInfo = new ProcessStartInfo(ClientExePath.Text)
                {
                    UseShellExecute = true,
                }
            };
            _clientProcess.Start();
        }

        private void StopSession()
        {
            // Dừng Proxy
            _cts?.Cancel();
            _proxyListener?.Stop();

            // Đóng các process
            try
            {
                if (_clientProcess != null && !_clientProcess.HasExited) _clientProcess.Kill();
            }
            catch
            {
            }

            try
            {
                if (_serverProcess != null && !_serverProcess.HasExited) _serverProcess.Kill();
            }
            catch
            {
            }

            _isSessionRunning = false;
            StartStopButton.Content = "Start Grading Session";
        }

        private async Task ListenForRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _proxyListener.GetContextAsync();
                    // Xử lý request trong một task mới để không block listener
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener đã bị dừng, thoát khỏi vòng lặp
                    break;
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var logEntry = new LoggedRequest
            {
                Method = request.HttpMethod,
                Url = request.Url.ToString()
            };

            // Đọc request body
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                logEntry.RequestBody = await reader.ReadToEndAsync();
            }

            // Chuyển tiếp request đến server thật
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

                    // Copy body
                    forwardRequest.Content =
                        new StringContent(logEntry.RequestBody, contentType);

                    // Gửi request và nhận response
                    var responseMessage = await client.SendAsync(forwardRequest);
                    var response = context.Response;

                    // Ghi log status code
                    logEntry.StatusCode = (int)responseMessage.StatusCode;

                    // Đọc response body
                    var responseContent = await responseMessage.Content.ReadAsByteArrayAsync();
                    logEntry.ResponseBody = Encoding.UTF8.GetString(responseContent);


                    // Copy response từ server thật về cho client gốc
                    response.StatusCode = (int)responseMessage.StatusCode;
                    response.ContentLength64 = responseContent.Length;
                    response.ContentType = responseMessage.Content.Headers.ContentType?.ToString();

                    await response.OutputStream.WriteAsync(responseContent, 0, responseContent.Length);
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            // Cập nhật UI từ thread chính
            Application.Current.Dispatcher.Invoke(() => { LoggedRequests.Add(logEntry); });
        }

        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            if (button?.DataContext is LoggedRequest loggedRequest)
            {
                StringBuilder msg = new StringBuilder();

                if (loggedRequest.Timestamp.Length > 0)
                {
                    msg.Append($"Time stamp: {loggedRequest.Timestamp}\n");
                }

                if (loggedRequest.Method.Length > 0)
                {
                    msg.Append($"Method: {loggedRequest.Method}\n");
                }

                if (loggedRequest.Url.Length > 0)
                {
                    msg.Append($"Url: {loggedRequest.Url}\n");
                }

                if (loggedRequest.StatusCode > 0)
                {
                    msg.Append($"Status code: {loggedRequest.StatusCode}\n");
                }

                if (loggedRequest.ResponseBody.Length > 0)
                {
                    msg.Append($"Response body: {loggedRequest.ResponseBody}\n");
                }

                MessageBox.Show(msg.ToString(), "Captured Data", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ReplaceAppSetting()
        {
            string templatePath = AppSettingTemplate.Text;
            string destinationDir = Path.GetDirectoryName(ServerExePath.Text);
        
            if (!File.Exists(templatePath) || !Directory.Exists(destinationDir))
            {
                MessageBox.Show("Folder/File does not exist!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        
            string[] searchResults = Directory.GetFiles(destinationDir, TARGET_FILE, SearchOption.AllDirectories);
            foreach (string item in searchResults)
            {
                try
                {
                    File.Copy(templatePath, item, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void AppsettingClientTemplate_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AppSettingClientTemplate.Text = openFileDialog.FileName;
            }
        }

    }
}