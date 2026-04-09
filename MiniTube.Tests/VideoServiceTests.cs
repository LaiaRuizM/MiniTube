using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MiniTube.Data;
using MiniTube.Models;
using MiniTube.Services;

namespace MiniTube.Tests;

public class VideoServiceTests
{
    // ─── Test helpers ───────────────────────────────────────────────────────
    //
    // BuildService spins up a fresh in-memory database for each test, so tests
    // are fully isolated from each other (no shared state). The unique
    // database name guarantees no cross-contamination.
    //
    // VideoService needs IConfiguration and ILogger<VideoService> too. The
    // related-videos logic doesn't read from either, so we pass an empty
    // configuration and a NullLogger (a built-in no-op logger).
    private static (VideoService Service, MiniTubeDbContext Db) BuildService()
    {
        var options = new DbContextOptionsBuilder<MiniTubeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new MiniTubeDbContext(options);
        var config = new ConfigurationBuilder().Build();
        var logger = NullLogger<VideoService>.Instance;

        return (new VideoService(db, config, logger), db);
    }

    private static VideoMetadata MakeVideo(string title, string category, string description = "")
        => new()
        {
            Title = title,
            Category = category,
            Description = description,
            FileName = $"{Guid.NewGuid()}.mp4",
            UploadedAt = DateTime.UtcNow
        };

    // ─── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRelatedVideosAsync_PrefersSameCategory()
    {
        // Arrange — create a fresh DB with a target video and 3 candidates,
        // only one of which shares the target's category ("Music").
        var (service, db) = BuildService();

        var target = MakeVideo("Target song", "Music");
        var sameCategory = MakeVideo("Another song", "Music");
        var differentCategory1 = MakeVideo("How to code", "Tech");
        var differentCategory2 = MakeVideo("Funny cats", "Entertainment");

        db.Videos.AddRange(target, sameCategory, differentCategory1, differentCategory2);
        await db.SaveChangesAsync();

        // Act — ask for the related videos for the target
        var related = await service.GetRelatedVideosAsync(target.Id);

        // Assert — the same-category video should be ranked first
        related.Should().NotBeEmpty();
        related.First().Id.Should().Be(sameCategory.Id);
    }

    [Fact]
    public async Task GetRelatedVideosAsync_ExcludesCurrentVideo()
    {
        // Arrange — create a target plus two unrelated videos
        var (service, db) = BuildService();

        var target = MakeVideo("My video", "Tech");
        var other1 = MakeVideo("Some other video", "Tech");
        var other2 = MakeVideo("Yet another", "Music");

        db.Videos.AddRange(target, other1, other2);
        await db.SaveChangesAsync();

        // Act
        var related = await service.GetRelatedVideosAsync(target.Id);

        // Assert — the target itself should never appear in its own related list
        related.Should().NotContain(v => v.Id == target.Id);
    }

    [Fact]
    public async Task GetRelatedVideosAsync_ScoresSharedKeywordsHigher()
    {
        // Arrange — all candidates share the same category, so the only
        // tiebreaker is shared keywords in the title/description.
        var (service, db) = BuildService();

        var target = MakeVideo("Learn ASP.NET Razor Pages", "Tech",
                               "A tutorial about Razor Pages and Entity Framework");

        // High overlap — shares "razor", "pages", "tutorial", "entity"
        var highMatch = MakeVideo("Razor Pages tutorial", "Tech",
                                  "Entity Framework deep dive");

        // Low overlap — shares no meaningful words
        var lowMatch = MakeVideo("Cooking pasta", "Tech",
                                 "Italian recipe");

        db.Videos.AddRange(target, highMatch, lowMatch);
        await db.SaveChangesAsync();

        // Act
        var related = await service.GetRelatedVideosAsync(target.Id);

        // Assert — the keyword-rich match should rank above the unrelated one
        related.Should().HaveCount(2);
        related[0].Id.Should().Be(highMatch.Id);
        related[1].Id.Should().Be(lowMatch.Id);
    }
}
