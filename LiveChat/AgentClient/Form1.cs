using System.IO;
using System.Net.Sockets;
using Microsoft.Web.WebView2.WinForms;
using Shared;

namespace AgentClient
{
    public partial class Form1 : Form
    {
        private WebView2 _webView = null!;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;

        private const string SERVER_IP = "127.0.0.1";
        private const int SERVER_PORT = 5050;

        public Form1()
        {
            InitializeComponent();
            this.Text = "Agent Dashboard - Tổng Đài Chăm Sóc Khách Hàng";
            this.ClientSize = new System.Drawing.Size(1150, 720);
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            _webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(_webView);

            await _webView.EnsureCoreWebView2Async(null);

            _webView.CoreWebView2.WebMessageReceived += async (sender, args) =>
            {
                string jsonFromUi = args.TryGetWebMessageAsString();

                if (_tcpClient == null || !_tcpClient.Connected)
                {
                    await ConnectToServerAsync();
                }

                if (_stream != null && _tcpClient != null && _tcpClient.Connected)
                {
                    try
                    {
                        await Packet.SendJsonAsync(_stream, jsonFromUi);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Lỗi gửi gói tin: " + ex.Message);
                    }
                }
            };

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent.html");
            _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);

            _webView.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                await ConnectToServerAsync();
            };
        }

        private async Task ConnectToServerAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(SERVER_IP, SERVER_PORT);
                _stream = _tcpClient.GetStream();

                this.Text = $"Agent Dashboard - Đã kết nối Server ({SERVER_IP}:{SERVER_PORT})";
                SendServerStatusToUi(true);

                _ = Task.Run(ReceiveLoopAsync);
            }
            catch
            {
                this.Text = "Agent Dashboard - Chế độ Offline (Chưa bật Server)";
                SendServerStatusToUi(false);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (_tcpClient != null && _tcpClient.Connected && _stream != null)
                {
                    string? jsonReceived = await Packet.ReceiveJsonAsync(_stream);
                    if (jsonReceived == null) break;

                    if (!this.IsDisposed)
                    {
                        this.Invoke(new Action(() =>
                        {
                            _webView.CoreWebView2.PostWebMessageAsJson(jsonReceived);
                        }));
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (!this.IsDisposed)
                {
                    this.Invoke(new Action(() =>
                    {
                        this.Text = "Agent Dashboard - Mất kết nối với Server!";
                        SendServerStatusToUi(false);
                    }));
                }
            }
        }

        private void SendServerStatusToUi(bool isConnected)
        {
            if (_webView.CoreWebView2 != null)
            {
                string statusJson = $"{{\"Type\":\"SYSTEM_CONN_STATUS\",\"Connected\":{(isConnected ? "true" : "false")}}}";
                _webView.CoreWebView2.PostWebMessageAsJson(statusJson);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch { }
            base.OnFormClosing(e);
        }
    }
}