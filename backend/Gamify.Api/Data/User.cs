namespace Gamify.Api.Data;

public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? PcLevel { get; set; }
    public DateOnly? BirthDate { get; set; }
    public List<Platform> Platforms { get; set; } = [];
    public List<GroupMember> GroupMembers { get; set; } = [];
    public List<SavedPlaylist> SavedPlaylists { get; set; } = [];
}
