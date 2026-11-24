namespace Gamify.Api.Data;

/// <summary>Vienas paspaudimas ant parduotuvės nuorodos – administratoriaus statistikai (GAM-US12).</summary>
public sealed class StoreClick
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int GameId { get; set; }
    public string Store { get; set; } = "";
    public DateTime ClickedAt { get; set; }
}
