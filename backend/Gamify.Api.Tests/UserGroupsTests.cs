using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Gamify.Api;
using Gamify.Api.Ai;
using Gamify.Api.Controllers;
using Gamify.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace Gamify.Api.Tests;

public sealed class UserGroupsTests
{
    [Fact]
    public void FromMembers_AggregatesSharedPlatformsStrictestAgeLowestPcAndSavedPreferences()
    {
        var users = new[]
        {
            Member(1, "Requester", new DateOnly(1990, 1, 1), [Platform.PcId, Platform.PlayStationId],
                "Aukštas", 2500, "Experienced"),
            Member(2, "Aistė", new DateOnly(2010, 1, 1), [Platform.PcId, Platform.SwitchId],
                "Žemas", 1200, "Beginner")
        };

        var profile = GroupRecommendationProfile.FromMembers(users, 1, new DateOnly(2026, 11, 24));

        Assert.Equal([Platform.PcId], profile.SharedPlatformIds);
        Assert.Equal(1, profile.PcLevel);
        Assert.Equal(AgeRatings.AgeOn(new DateOnly(2010, 1, 1), new DateOnly(2026, 11, 24)), profile.Age);
        Assert.Equal(2, profile.GroupSize);
        Assert.Equal("Jūs", profile.Participants[0].Name);
        Assert.Equal("Aistė", profile.Participants[1].Name);
        Assert.Equal(1200, profile.Participants[1].MaxBudgetCents);
        Assert.Equal(ExperienceLevel.Beginner, profile.Participants[1].Experience);
    }

    [Fact]
    public async Task Create_AddsCreatorAsFirstMember()
    {
        await using var test = await TestDatabase.Create();
        var owner = await test.AddUser(1, "Owner", [Platform.PcId], "Vidutinis");
        await test.Db.SaveChangesAsync();
        var controller = test.GroupsController(1);

        var response = await controller.Create(new CreateGroupRequest("Draugai"), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var group = Assert.IsType<GroupResponse>(created.Value);
        Assert.Single(group.Members);
        Assert.Equal(owner.Id, group.Members[0].Id);
        Assert.Equal(owner.Email, group.Members[0].Email);
    }

    [Fact]
    public async Task Get_ReturnsOwnedGroupsWithSavedMemberPreferences()
    {
        await using var test = await TestDatabase.Create();
        await test.AddUser(1, "Owner", [Platform.PcId], "Vidutinis");
        var controller = test.GroupsController(1);
        var created = Assert.IsType<ObjectResult>(
            await controller.Create(new CreateGroupRequest("Draugai"), CancellationToken.None));
        var group = Assert.IsType<GroupResponse>(created.Value);

        var response = await controller.Get(CancellationToken.None);

        var groups = Assert.IsAssignableFrom<IEnumerable<GroupResponse>>(Assert.IsType<OkObjectResult>(response).Value);
        var listedGroup = Assert.Single(groups);
        Assert.Equal(group.Id, listedGroup.Id);
        Assert.Equal("Owner", Assert.Single(listedGroup.Members).Name);
        Assert.Equal("Vidutinis", listedGroup.Members[0].PcLevel);
    }

    [Fact]
    public async Task AddMember_RejectsMoreThanFiveUsersIncludingCreator()
    {
        await using var test = await TestDatabase.Create();
        for (var id = 1; id <= 6; id++)
            await test.AddUser(id, $"User {id}", [Platform.PcId], "Vidutinis");
        await test.Db.SaveChangesAsync();

        var controller = test.GroupsController(1);
        var createResponse = Assert.IsType<ObjectResult>(
            await controller.Create(new CreateGroupRequest("Draugai"), CancellationToken.None));
        var group = Assert.IsType<GroupResponse>(createResponse.Value);
        for (var id = 2; id <= 5; id++)
        {
            var response = await controller.AddMember(group.Id, new AddGroupMemberRequest(id), CancellationToken.None);
            Assert.IsType<NoContentResult>(response);
        }

        var sixthMember = await controller.AddMember(
            group.Id, new AddGroupMemberRequest(6), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(sixthMember);
        Assert.Equal(5, await test.Db.UserGroupMembers.CountAsync(member => member.GroupId == group.Id));
    }

    [Fact]
    public async Task SearchForGroup_ReportsMembersWhenPlatformsDoNotOverlap()
    {
        await using var test = await TestDatabase.Create();
        await test.AddUser(1, "Owner", [Platform.PcId], "Aukštas");
        await test.AddUser(2, "Aistė", [Platform.PlayStationId], "Vidutinis");
        await test.Db.SaveChangesAsync();
        var groups = test.GroupsController(1);
        var created = Assert.IsType<ObjectResult>(
            await groups.Create(new CreateGroupRequest("Draugai"), CancellationToken.None));
        var group = Assert.IsType<GroupResponse>(created.Value);
        Assert.IsType<NoContentResult>(await groups.AddMember(
            group.Id, new AddGroupMemberRequest(2), CancellationToken.None));

        var ai = new OpenAiResponsesClient(new HttpClient(), Options.Create(new AiOptions()));
        var search = new LiveRecommendations(ai, new LiveStoreClient(new HttpClient()),
            NullLogger<LiveRecommendations>.Instance);
        var controller = new RecommendationsController(test.Db, ai, search,
            new RecommendationTickets(new EphemeralDataProtectionProvider()))
        {
            ControllerContext = TestDatabase.ControllerContext(1)
        };

        var response = await controller.Search(
            new RecommendationRequest("Co-op žaidimas", true, group.Id), CancellationToken.None);

        var result = JsonSerializer.SerializeToElement(
            Assert.IsType<OkObjectResult>(response).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Empty(result.GetProperty("games").EnumerateArray());
        var blockers = result.GetProperty("blockingMembers").EnumerateArray().ToArray();
        Assert.Equal(["Jūs", "Aistė"], blockers.Select(member => member.GetProperty("name").GetString()));
        Assert.All(blockers, member =>
            Assert.Equal("platformos", member.GetProperty("constraints")[0].GetString()));
    }

    private static User Member(
        int id, string name, DateOnly birthDate, int[] platformIds,
        string? pcLevel, int? budgetCents = null, string? experience = null) =>
        new()
        {
            Id = id,
            Name = name,
            Email = $"{id}@example.test",
            NormalizedEmail = $"{id}@EXAMPLE.TEST",
            PasswordHash = "not-a-real-hash",
            BirthDate = birthDate,
            Platforms = platformIds.Select(id => new Platform { Id = id, Name = PlatformName(id) }).ToList(),
            PcLevel = pcLevel,
            PreferredBudgetCents = budgetCents,
            PreferredExperience = experience
        };

    private static string PlatformName(int id) => id switch
    {
        Platform.PcId => "PC",
        Platform.PlayStationId => "PlayStation",
        Platform.XboxId => "Xbox",
        Platform.SwitchId => "Switch",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public GamifyDbContext Db { get; }

        private TestDatabase(SqliteConnection connection, GamifyDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<TestDatabase> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<GamifyDbContext>().UseSqlite(connection).Options;
            var db = new GamifyDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, db);
        }

        public UserGroupsController GroupsController(int userId) => new(Db)
        {
            ControllerContext = ControllerContext(userId)
        };

        public async Task<User> AddUser(int id, string name, int[] platformIds, string? pcLevel)
        {
            var platforms = await Db.Platforms.Where(platform => platformIds.Contains(platform.Id)).ToListAsync();
            var user = Member(id, name, new DateOnly(1990, 1, 1), platformIds, pcLevel);
            user.Platforms = platforms;
            Db.Users.Add(user);
            return user;
        }

        public static ControllerContext ControllerContext(int userId) => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                ], "test"))
            }
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
