namespace Gamify.Api.Data;

public sealed class QueryTemplate
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}