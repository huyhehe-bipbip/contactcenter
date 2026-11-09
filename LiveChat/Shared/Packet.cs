namespace Shared;

public enum MessageType
{
    ClientInit,
    AgentReady,
    SessionAssigned,
    ChatMessage
}

public class Packet
{
    public MessageType Type { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(this);
    public static Packet? Deserialize(string json) => System.Text.Json.JsonSerializer.Deserialize<Packet>(json);
}
