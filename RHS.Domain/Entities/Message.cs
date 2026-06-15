namespace RHS.Domain.Entities;

public class Message
{
    public Guid MessageId { get; set; }

    public Guid SenderId { get; set; }

    public Guid ReceiverId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    // Navigation properties
    public User Sender { get; set; } = null!;

    public User Receiver { get; set; } = null!;
}
