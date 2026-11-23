namespace Gamify.Api.Data;

public sealed class UserSession
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
