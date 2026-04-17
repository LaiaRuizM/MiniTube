using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MiniTube.Data;
using MiniTube.Models;
using MiniTube.Pages;
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
    private static (VideoService Service, LikeService LikeService, MiniTubeDbContext Db) BuildService()
    {
        var options = new DbContextOptionsBuilder<MiniTubeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new MiniTubeDbContext(options);
        var config = new ConfigurationBuilder().Build();
        var logger = NullLogger<VideoService>.Instance;

        return (new VideoService(db, config, logger), new LikeService(db), db);
    }

    private static VideoMetadata MakeVideo(
        string title, string category, string description = "",
        string? ownerEmail = null)
        => new()
        {
            Title = title,
            Category = category,
            Description = description,
            FileName = $"{Guid.NewGuid()}.mp4",
            UploadedAt = DateTime.UtcNow,
            OwnerEmail = ownerEmail
        };

    // ─── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRelatedVideosAsync_PrefersSameCategory()
    {
        // Arrange — create a fresh DB with a target video and 3 candidates,
        // only one of which shares the target's category ("Music").
        var (service, likeService, db) = BuildService();

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
        var (service, likeService, db) = BuildService();

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
        var (service, likeService, db) = BuildService();

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

    // ─── CanUserEditAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CanUserEditAsync_AdminCanEditAnyVideo()
    {
        var (service, likeService, db) = BuildService();
        var video = MakeVideo("Someone's video", "Tech", ownerEmail: "owner@example.com");
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var result = await service.CanUserEditAsync(video.Id, "admin@example.com", isAdmin: true);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserEditAsync_OwnerCanEditOwnVideo()
    {
        var (service, likeService, db) = BuildService();
        var video = MakeVideo("My video", "Tech", ownerEmail: "owner@example.com");
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var result = await service.CanUserEditAsync(video.Id, "owner@example.com", isAdmin: false);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserEditAsync_StrangerCannotEditOthersVideo()
    {
        var (service, likeService, db) = BuildService();
        var video = MakeVideo("Not yours", "Tech", ownerEmail: "owner@example.com");
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var result = await service.CanUserEditAsync(video.Id, "stranger@example.com", isAdmin: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserEditAsync_AnonymousUserCannotEdit()
    {
        var (service, likeService, db) = BuildService();
        var video = MakeVideo("Any video", "Tech", ownerEmail: "owner@example.com");
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var result = await service.CanUserEditAsync(video.Id, null, isAdmin: false);

        result.Should().BeFalse();
    }

    // ─── ToggleLikeAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ToggleLikeAsync_FirstLikeIsRecorded()
    {
        var (service, likeService, db) = BuildService();
        var video = MakeVideo("Cool video", "Tech");
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        await likeService.ToggleLikeAsync(video.Id, "user@example.com", isLike: true);

        var info = await likeService.GetLikeInfoAsync(video.Id, "user@example.com");
        info.Likes.Should().Be(1);
        info.Dislikes.Should().Be(0);
        info.UserVote.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleLikeAsync_LikeThenDislikeSwitchesVote()
    {
        var (service, likeService, db) = BuildService();
        var video = MakeVideo("Cool video", "Tech");
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        await likeService.ToggleLikeAsync(video.Id, "user@example.com", isLike: true);
        await likeService.ToggleLikeAsync(video.Id, "user@example.com", isLike: false);

        var info = await likeService.GetLikeInfoAsync(video.Id, "user@example.com");
        info.Likes.Should().Be(0);
        info.Dislikes.Should().Be(1);
        info.UserVote.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleLikeAsync_DoubleLikeRemovesVote()
    {
        var (service, likeService, db) = BuildService();
        var video = MakeVideo("Cool video", "Tech");
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        await likeService.ToggleLikeAsync(video.Id, "user@example.com", isLike: true);
        await likeService.ToggleLikeAsync(video.Id, "user@example.com", isLike: true);

        var info = await likeService.GetLikeInfoAsync(video.Id, "user@example.com");
        info.Likes.Should().Be(0);
        info.Dislikes.Should().Be(0);
        info.UserVote.Should().BeNull();
    }

    // ─── SaveVideoAsync — file extension validation ────────────────────────

    [Fact]
    public async Task SaveVideoAsync_RejectsExeFile()
    {
        var (service, _, _) = BuildService();
        var form = new UploadForm
        {
            Title = "Malware",
            Category = "Tech",
            VideoFile = new FormFileFake("malware.exe", 1024)
        };

        var act = () => service.SaveVideoAsync(form, "user@example.com", "User");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not allowed*");
    }

    // ─── Upload page — oversized file validation ───────────────────────────

    [Fact]
    public async Task UploadPage_RejectsOversizedFile()
    {
        var (service, _, _) = BuildService();
        var pageModel = new UploadModel(service);

        // Set up minimal PageContext so ModelState works
        pageModel.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };

        // File reports 501MB but only has 1KB of real data
        var oversizedFile = new FormFileFake("big-video.mp4", 1024,
            reportedLength: 501L * 1024 * 1024);

        pageModel.Form = new UploadForm
        {
            Title = "Big Video",
            Category = "Tech",
            VideoFile = oversizedFile
        };

        var result = await pageModel.OnPostAsync();

        // Should re-render the page (not redirect to /Index)
        result.Should().BeOfType<PageResult>();
        pageModel.ModelState.IsValid.Should().BeFalse();
        pageModel.ModelState["Form.VideoFile"]!.Errors
            .Should().Contain(e => e.ErrorMessage.Contains("500 MB"));
    }

    // ─── GetBlobSasUrl — when no Blob Storage configured ───────────────────

    [Fact]
    public void GetBlobSasUrl_ReturnsNull_WhenBlobStorageNotConfigured()
    {
        var (service, _, _) = BuildService();

        var result = service.GetBlobSasUrl("any-file.mp4");

        result.Should().BeNull();
    }
}

/// <summary>
/// Minimal IFormFile implementation for testing upload validation
/// without needing a real file or HTTP request.
/// </summary>
internal class FormFileFake : IFormFile
{
    private readonly byte[] _content;
    private readonly long _reportedLength;
    public string FileName { get; }
    public string ContentType => "application/octet-stream";
    public long Length => _reportedLength;
    public string Name => "file";
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();

    public FormFileFake(string fileName, int sizeBytes, long? reportedLength = null)
    {
        FileName = fileName;
        _content = new byte[sizeBytes];
        _reportedLength = reportedLength ?? sizeBytes;
    }

    public Stream OpenReadStream() => new MemoryStream(_content);
    public void CopyTo(Stream target) => OpenReadStream().CopyTo(target);
    public Task CopyToAsync(Stream target, CancellationToken ct = default)
        => OpenReadStream().CopyToAsync(target, ct);
}
