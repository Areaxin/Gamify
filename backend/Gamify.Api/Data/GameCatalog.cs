using Microsoft.EntityFrameworkCore;

namespace Gamify.Api.Data;

internal static class GameCatalog
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        const int pc = Platform.PcId;
        const int ps = Platform.PlayStationId;
        const int xbox = Platform.XboxId;
        const int @switch = Platform.SwitchId;
        int[] all = [pc, ps, xbox, @switch];

        (Game Game, int[] PlatformIds)[] entries =
        [
            (Game(1, "Stardew Valley", 1399, 4, GameMood.Relaxing, 1, 1, pegi: 12, esrb: 10), all),
            (Game(2, "Farm Together", 1999, 4, GameMood.Relaxing, 1, 1, pegi: 3, esrb: null), all),
            (Game(3, "Human Fall Flat", 1999, 4, GameMood.Relaxing | GameMood.Puzzle, 1, 1, pegi: 3, esrb: 0), all),
            (Game(4, "Untitled Goose Game", 1999, 2, GameMood.Relaxing | GameMood.Puzzle, 1, 1, pegi: 3, esrb: 0), all),
            (Game(5, "Cat Quest II", 1499, 2, GameMood.Relaxing | GameMood.Adventure, 1, 1, pegi: 7, esrb: 10), all),
            (Game(6, "Palia", 0, 4, GameMood.Relaxing | GameMood.Adventure, 1, 2, pegi: 12, esrb: 10), [pc, @switch]),
            (Game(7, "Fae Farm", 2999, 4, GameMood.Relaxing | GameMood.Adventure, 1, 2, pegi: 7, esrb: 10), [pc, @switch]),
            (Game(8, "Unpacking", 1999, 1, GameMood.Relaxing | GameMood.Puzzle, 1, 1, pegi: 3, esrb: 0), all),
            (Game(9, "Snipperclips", 1999, 4, GameMood.Relaxing | GameMood.Puzzle, 1, null, pegi: 3, esrb: 0), [@switch]),
            (Game(10, "Minecraft", 2999, 4, GameMood.Relaxing | GameMood.Adventure, 1, 2, pegi: 7, esrb: 10), all),
            (Game(11, "Minecraft Dungeons", 1999, 4, GameMood.Adventure | GameMood.Action, 1, 2, pegi: 7, esrb: 10), all),
            (Game(12, "Rocket League", 0, 4, GameMood.Competitive | GameMood.Party, 2, 2, pegi: 3, esrb: 0), all),
            (Game(13, "Portal 2", 975, 2, GameMood.Puzzle, 2, 1, pegi: 12, esrb: 10), [pc, @switch]),
            (Game(14, "World of Goo 2", 2999, 2, GameMood.Puzzle | GameMood.Strategy, 2, 1, pegi: null, esrb: 13), [pc, @switch]),
            (Game(15, "Left 4 Dead 2", 975, 4, GameMood.Action, 2, 1, pegi: 18, esrb: 17), [pc]),
            (Game(16, "DOOM (2016)", 1999, 1, GameMood.Action, 3, 3, pegi: 18, esrb: 17), all)
        ];

        foreach (var (game, _) in entries)
            game.StoreUrl = StoreUrls.GetValueOrDefault(game.Id);

        modelBuilder.Entity<Game>().HasData(entries.Select(entry => entry.Game));
        modelBuilder.Entity<Game>().HasMany(game => game.Platforms).WithMany().UsingEntity("GamePlatforms",
            join => join.HasData(entries.SelectMany(entry => entry.PlatformIds.Select(platformId =>
                new { GameId = entry.Game.Id, PlatformsId = platformId }))));
    }

    // Oficialūs parduotuvių puslapiai. Minecraft ir Snipperclips nėra Steam / Epic, Fae Farm nuoroda dar nepatikrinta,
    // todėl jų mygtukas sąsajoje neaktyvus.
    private static readonly Dictionary<int, string> StoreUrls = new()
    {
        [1] = StoreLinks.Steam(413150),            // Stardew Valley
        [2] = StoreLinks.Steam(673950),            // Farm Together
        [3] = StoreLinks.Steam(477160),            // Human Fall Flat
        [4] = StoreLinks.Steam(837470),            // Untitled Goose Game
        [5] = StoreLinks.Steam(914710),            // Cat Quest II
        [6] = StoreLinks.Steam(2707930),           // Palia
        [8] = StoreLinks.Steam(1135690),           // Unpacking
        [11] = StoreLinks.Steam(1672970),          // Minecraft Dungeons
        [12] = StoreLinks.Epic("rocket-league"),   // Rocket League (nemokamas tik per Epic)
        [13] = StoreLinks.Steam(620),              // Portal 2
        [14] = StoreLinks.Epic("world-of-goo-2"),  // World of Goo 2
        [15] = StoreLinks.Steam(550),              // Left 4 Dead 2
        [16] = StoreLinks.Steam(379720)            // DOOM (2016)
    };

    private static Game Game(int id, string title, int priceCents, int maxPlayers, GameMood moods, int difficulty,
        int? minimumPcLevel, int? pegi, int? esrb) => new()
    {
        Id = id,
        Title = title,
        PriceCents = priceCents,
        MaxPlayers = maxPlayers,
        Moods = moods,
        Difficulty = difficulty,
        MinimumPcLevel = minimumPcLevel,
        PegiAge = pegi,
        EsrbAge = esrb
    };
}
