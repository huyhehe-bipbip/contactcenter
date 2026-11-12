using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Shared
{
    // Cấu trúc gói tin JSON chuẩn theo Mục III của tài liệu đặc tả
    public class Packet
    {
        public string Type { get; set; } = string.Empty;        // AGENT_LOGIN, CUSTOMER_REQ, SESSION_ASSIGNED, CHAT_MSG, END_SESSION
        public string? Session_ID { get; set; }                 // ID phiên chat
        public string? Agent_ID { get; set; }                   // ID của nhân viên
        public string? Customer_ID { get; set; }                // ID của khách hàng
        public string? Customer_Name { get; set; }              // Tên hiển thị của khách hàng
        public string? Sender { get; set; }                     // Người gửi tin nhắn
        public string? Content { get; set; }                    // Nội dung tin nhắn

        // Chuyển Object thành mảng Byte có gắn 4 bytes độ dài ở đầu
        public static byte[] Encode(string jsonPayload)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
            byte[] lengthPrefix = BitConverter.GetBytes(payloadBytes.Length); // 4 bytes

            byte[] finalPacket = new byte[4 + payloadBytes.Length];
            Buffer.BlockCopy(lengthPrefix, 0, finalPacket, 0, 4);
            Buffer.BlockCopy(payloadBytes, 0, finalPacket, 4, payloadBytes.Length);

            return finalPacket;
        }

        // Gửi gói tin qua NetworkStream 
        public static async Task SendJsonAsync(NetworkStream stream, string jsonPayload)
        {
            byte[] packetBytes = Encode(jsonPayload);
            await stream.WriteAsync(packetBytes, 0, packetBytes.Length);
            await stream.FlushAsync();
        }

        // Nhận gói tin từ NetworkStream 
        public static async Task<string?> ReceiveJsonAsync(NetworkStream stream)
        {
            byte[] lengthBuffer = new byte[4];
            int headerRead = await ReadExactBytesAsync(stream, lengthBuffer, 4);
            if (headerRead == 0) return null; // Đã ngắt kết nối

            int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength <= 0) return null;

            byte[] payloadBuffer = new byte[payloadLength];
            int payloadRead = await ReadExactBytesAsync(stream, payloadBuffer, payloadLength);
            if (payloadRead == 0) return null;

            return Encoding.UTF8.GetString(payloadBuffer);
        }

        // Hàm đảm bảo đọc đủ số byte yêu cầu 
        private static async Task<int> ReadExactBytesAsync(NetworkStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
                if (read == 0) return 0; 
                totalRead += read;
            }
            return totalRead;
        }
    }
}