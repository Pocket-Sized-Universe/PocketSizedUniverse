namespace PocketSizedUniverse.Data;

public class ChatMessage
{
    public Guid ChatId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}