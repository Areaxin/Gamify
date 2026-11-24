namespace Gamify.Api.Data;

public sealed class UserGroup
{
    public int Id { get; set; }
    public int OwnerUserId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<UserGroupMember> Members { get; set; } = [];
}

public sealed class UserGroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public UserGroup Group { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}
