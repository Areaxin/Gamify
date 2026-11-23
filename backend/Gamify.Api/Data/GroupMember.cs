namespace Gamify.Api.Data;

public sealed class GroupMember
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly BirthDate { get; set; }
}
