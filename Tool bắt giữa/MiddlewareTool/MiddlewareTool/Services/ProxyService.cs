// MiddlewareTool/Services/ProxyService.cs
using MiddlewareTool.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MiddlewareTool.Services
{
    public class ProxyService
    {
        private readonly int _proxyPort;
        private readonly int _realServerPort;
        private readonly ObservableCollection<LoggedRequest> _loggedRequests;
        private readonly ExcelLogger _excelLogger;

        private HttpListener? _httpListener;
        private TcpListener? _tcpListener;

        public ProxyService(int proxyPort, int realServerPort, ObservableCollection<LoggedRequest> loggedRequests, ExcelLogger excelLogger)
        {
            _proxyPort = proxyPort;
            _realServerPort = realServerPort;
            _loggedRequests = loggedRequests;
            _excelLogger = excelLogger;
        }

        public void StartProxy(string protocol, CancellationToken token)
        {
            if (protocol == "HTTP")
            {
                StartHttpProxy(token);
            }
            else // TCP
            {
                StartTcpProxy(token);
            }
        }

        public void StopProxy()
        {
            _httpListener?.Stop();
            _tcpListener?.Stop();
        }

        private void StartHttpProxy(CancellationToken token)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_proxyPort}/");
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
                var realServerUrl = $"http://localhost:{_realServerPort}{request.Url.AbsolutePath}";
                using (var client = new HttpClient())
                {
                    var forwardRequest = new HttpRequestMessage(new HttpMethod(request.HttpMethod), realServerUrl);
                    MediaTypeHeaderValue? contentType = null;
                    if (request.ContentType != null)
                    {
                        contentType = MediaTypeHeaderValue.Parse(request.ContentType);
                    }
                    forwardRequest.Content = new StringContent(logEntry.RequestBody, request.ContentEncoding, contentType?.MediaType);
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
            _excelLogger.AppendToExcelLog(logEntry);
            Application.Current.Dispatcher.Invoke(() => _loggedRequests.Add(logEntry));
        }

        private void StartTcpProxy(CancellationToken token)
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, _proxyPort);
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
                    await serverConnection.ConnectAsync(IPAddress.Loopback, _realServerPort, token);
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
                _excelLogger.AppendToExcelLog(logEntry);
                Application.Current.Dispatcher.Invoke(() => _loggedRequests.Add(logEntry));
            }
        }
    }
}