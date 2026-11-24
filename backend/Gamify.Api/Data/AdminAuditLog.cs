namespace Gamify.Api.Data;

public sealed class AdminAuditLog
{
    public long Id { get; set; }
    public int AdminUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int AccountId { get; set; }
}
