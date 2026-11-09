using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Shared;

namespace CustomerClient
{
    public partial class Form1 : Form
    {
        private Button btnConnect;
        private Button btnSend;
        private TextBox txtLog;
        private TextBox txtInput;
        private TcpClient? tcpClient;
        private StreamReader? reader;
        private StreamWriter? writer;
        private string? agentId;
        private string? customerId;
        private CancellationTokenSource? cts;

        public Form1()
        {
            btnConnect = new Button { Text = "Connect", Left = 10, Top = 10, Width = 100 };
            btnSend = new Button { Text = "Send", Left = 320, Top = 320, Width = 80 };
            txtLog = new TextBox { Left = 10, Top = 50, Width = 390, Height = 250, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            txtInput = new TextBox { Left = 10, Top = 320, Width = 300, Height = 23 };
            Controls.Add(btnConnect);
            Controls.Add(btnSend);
            Controls.Add(txtLog);
            Controls.Add(txtInput);
            Width = 430;
            Height = 410;
            btnConnect.Click += BtnConnect_Click;
            btnSend.Click += BtnSend_Click;
            FormClosing += Form1_FormClosing;
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            btnConnect.Enabled = false;
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", 5000);
            var stream = tcpClient.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            customerId = $"CUST_{Guid.NewGuid().ToString("N")[..4]}";
            var initPacket = new Packet
            {
                Type = MessageType.ClientInit,
                SenderId = customerId
            };
            await writer.WriteLineAsync(initPacket.Serialize());
            cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenAsync(cts.Token));
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader!.ReadLineAsync();
                if (line == null) break;
                var packet = Packet.Deserialize(line);
                if (packet == null) continue;
                if (packet.Type == MessageType.SessionAssigned)
                {
                    agentId = packet.SenderId;
                    AppendLog($"[SESSION] Assigned to agent {agentId} (Session: {packet.SessionId})");
                }
                else if (packet.Type == MessageType.ChatMessage)
                {
                    AppendLog($"[AGENT] {packet.SenderId}: {packet.Content}");
                }
            }
        }

        private async void BtnSend_Click(object? sender, EventArgs e)
        {
            if (writer == null || string.IsNullOrEmpty(agentId)) return;
            var msg = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;
            var packet = new Packet
            {
                Type = MessageType.ChatMessage,
                SenderId = customerId ?? "",
                ReceiverId = agentId,
                Content = msg
            };
            await writer.WriteLineAsync(packet.Serialize());
            AppendLog($"[ME]: {msg}");
            txtInput.Clear();
        }

        private void AppendLog(string text)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke(new Action(() => AppendLog(text)));
            }
            else
            {
                txtLog.AppendText(text + Environment.NewLine);
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            cts?.Cancel();
            reader?.Dispose();
            writer?.Dispose();
            tcpClient?.Close();
        }
    }
}
