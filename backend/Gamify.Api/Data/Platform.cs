namespace Gamify.Api.Data;

public sealed class Platform
{
    public const int PcId = 1;
    public const int PlayStationId = 2;
    public const int XboxId = 3;
    public const int SwitchId = 4;

    public int Id { get; set; }
    public string Name { get; set; } = "";
}
