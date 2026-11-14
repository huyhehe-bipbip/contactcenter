using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Shared
{
    public class Packet
    {
        public string Type { get; set; } = string.Empty;
        public string? Session_ID { get; set; }
        public string? Agent_ID { get; set; }
        public string? Customer_ID { get; set; }
        public string? Customer_Name { get; set; }
        public string? Sender { get; set; }
        public string? Content { get; set; }
        public string? Attachment { get; set; }

        public static byte[] Encode(string jsonPayload)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
            byte[] lengthPrefix = BitConverter.GetBytes(payloadBytes.Length);

            byte[] finalPacket = new byte[4 + payloadBytes.Length];
            Buffer.BlockCopy(lengthPrefix, 0, finalPacket, 0, 4);
            Buffer.BlockCopy(payloadBytes, 0, finalPacket, 4, payloadBytes.Length);

            return finalPacket;
        }

        public static async Task SendJsonAsync(NetworkStream stream, string jsonPayload)
        {
            byte[] packetBytes = Encode(jsonPayload);
            await stream.WriteAsync(packetBytes, 0, packetBytes.Length);
            await stream.FlushAsync();
        }

        public static async Task<string?> ReceiveJsonAsync(NetworkStream stream)
        {
            byte[] lengthBuffer = new byte[4];
            int headerRead = await ReadExactBytesAsync(stream, lengthBuffer, 4);
            if (headerRead == 0) return null;

            int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength <= 0) return null;

            byte[] payloadBuffer = new byte[payloadLength];
            int payloadRead = await ReadExactBytesAsync(stream, payloadBuffer, payloadLength);
            if (payloadRead == 0) return null;

            return Encoding.UTF8.GetString(payloadBuffer);
        }

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