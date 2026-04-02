using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.EntityFrameworkCore;
using MiniTube.Data;
using MiniTube.Models;

namespace MiniTube.Services;

public class VideoService
{
    private readonly MiniTubeDbContext _db;
    private readonly BlobContainerClient? _blobContainer;
    private readonly ILogger<VideoService> _logger;

    private static readonly string[] AllowedExtensions = { ".mp4", ".webm", ".mov" };
    public static readonly string[] Categories = { "Education", "Entertainment", "Gaming", "Music", "Tech", "Other" };

    public VideoService(MiniTubeDbContext db, IConfiguration config, ILogger<VideoService> logger)
    {
        _db = db;
        _logger = logger;

        var blobConnectionString = config["AzureStorage:ConnectionString"];
        var containerName = config["AzureStorage:ContainerName"] ?? "videos";

        if (!string.IsNullOrEmpty(blobConnectionString))
        {
            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            _blobContainer = blobServiceClient.GetBlobContainerClient(containerName);
            _blobContainer.CreateIfNotExists();
        }
    }

    // Generate a temporary signed URL for private blobs (valid for 1 hour)
    public string? GetBlobSasUrl(string blobName)
    {
        if (_blobContainer == null) return null;

        var blobClient = _blobContainer.GetBlobClient(blobName);
        if (!blobClient.CanGenerateSasUri) return null;

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _blobContainer.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }

    public async Task<List<VideoMetadata>> GetAllAsync()
    {
        return await _db.Videos.ToListAsync();
    }

    public async Task<VideoMetadata?> GetByIdAsync(string id)
    {
        return await _db.Videos.FindAsync(id);
    }

    public async Task<bool> CanUserEditAsync(string videoId, string? userEmail, bool isAdmin)
    {
        if (isAdmin) return true;
        if (string.IsNullOrEmpty(userEmail)) return false;

        var video = await _db.Videos.FindAsync(videoId);
        if (video == null) return false;

        return video.OwnerEmail != null &&
               video.OwnerEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<VideoMetadata> SaveVideoAsync(UploadForm form, string? ownerEmail, string? ownerName)
    {
        var file = form.VideoFile!;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");

        var metadata = new VideoMetadata
        {
            Title = form.Title.Trim(),
            Description = form.Description?.Trim() ?? string.Empty,
            Category = form.Category,
            FileName = $"{Guid.NewGuid()}{extension}",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = file.Length,
            OwnerEmail = ownerEmail,
            OwnerName = ownerName
        };

        // Upload video to Blob Storage
        if (_blobContainer != null)
        {
            var blobClient = _blobContainer.GetBlobClient(metadata.FileName);
            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = GetContentType(extension)
            });
            metadata.BlobUrl = blobClient.Uri.ToString();
        }

        // Generate thumbnail and upload to Blob Storage
        await GenerateAndUploadThumbnailAsync(metadata, file);

        _db.Videos.Add(metadata);
        await _db.SaveChangesAsync();

        return metadata;
    }

    public async Task UpdateMetadataAsync(string id, string title, string? description, string category)
    {
        var video = await _db.Videos.FindAsync(id);
        if (video != null)
        {
            video.Title = title;
            video.Description = description ?? string.Empty;
            video.Category = category;
            await _db.SaveChangesAsync();
        }
    }

    public async Task DeleteVideoAsync(string id)
    {
        var video = await _db.Videos.FindAsync(id);
        if (video != null)
        {
            if (_blobContainer != null)
            {
                var blobClient = _blobContainer.GetBlobClient(video.FileName);
                await blobClient.DeleteIfExistsAsync();

                if (!string.IsNullOrEmpty(video.ThumbnailFileName))
                {
                    var thumbClient = _blobContainer.GetBlobClient($"thumbnails/{video.ThumbnailFileName}");
                    await thumbClient.DeleteIfExistsAsync();
                }
            }

            _db.Videos.Remove(video);
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdateVideoFileAsync(string id, IFormFile newVideoFile)
    {
        var extension = Path.GetExtension(newVideoFile.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");

        var video = await _db.Videos.FindAsync(id);
        if (video == null) return;

        if (_blobContainer != null)
        {
            var oldBlobClient = _blobContainer.GetBlobClient(video.FileName);
            await oldBlobClient.DeleteIfExistsAsync();

            if (!string.IsNullOrEmpty(video.ThumbnailFileName))
            {
                var oldThumbClient = _blobContainer.GetBlobClient($"thumbnails/{video.ThumbnailFileName}");
                await oldThumbClient.DeleteIfExistsAsync();
            }
        }

        var newFileName = $"{Guid.NewGuid()}{extension}";
        video.FileName = newFileName;
        video.FileSizeBytes = newVideoFile.Length;

        if (_blobContainer != null)
        {
            var blobClient = _blobContainer.GetBlobClient(newFileName);
            using var stream = newVideoFile.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = GetContentType(extension)
            });
            video.BlobUrl = blobClient.Uri.ToString();
        }

        await GenerateAndUploadThumbnailAsync(video, newVideoFile);
        await _db.SaveChangesAsync();
    }

    private async Task GenerateAndUploadThumbnailAsync(VideoMetadata metadata, IFormFile videoFile)
    {
        try
        {
            var tempVideoPath = Path.Combine(Path.GetTempPath(), metadata.FileName);
            var tempThumbPath = Path.Combine(Path.GetTempPath(), $"{metadata.Id}.jpg");

            if (!File.Exists(tempVideoPath))
            {
                using var tempStream = new FileStream(tempVideoPath, FileMode.Create);
                await videoFile.OpenReadStream().CopyToAsync(tempStream);
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -ss 2 -i \"{tempVideoPath}\" -vframes 1 -q:v 2 -update 1 \"{tempThumbPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0 && File.Exists(tempThumbPath))
                {
                    var thumbFileName = $"{metadata.Id}.jpg";
                    metadata.ThumbnailFileName = thumbFileName;

                    if (_blobContainer != null)
                    {
                        var thumbBlobClient = _blobContainer.GetBlobClient($"thumbnails/{thumbFileName}");
                        using var thumbStream = File.OpenRead(tempThumbPath);
                        await thumbBlobClient.UploadAsync(thumbStream, new BlobHttpHeaders
                        {
                            ContentType = "image/jpeg"
                        });
                        metadata.ThumbnailBlobUrl = thumbBlobClient.Uri.ToString();
                    }
                }
            }

            if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
            if (File.Exists(tempThumbPath)) File.Delete(tempThumbPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate thumbnail for video {VideoId}", metadata.Id);
        }
    }

    // --- Like / Dislike ---

    public async Task ToggleLikeAsync(string videoId, string userEmail, bool isLike)
    {
        var existing = await _db.VideoLikes
            .FirstOrDefaultAsync(l => l.VideoId == videoId && l.UserEmail == userEmail);

        if (existing != null)
        {
            if (existing.IsLike == isLike)
            {
                // Same button clicked again → remove the vote
                _db.VideoLikes.Remove(existing);
            }
            else
            {
                // Switch from like to dislike or vice versa
                existing.IsLike = isLike;
            }
        }
        else
        {
            // New vote
            _db.VideoLikes.Add(new VideoLike
            {
                VideoId = videoId,
                UserEmail = userEmail,
                IsLike = isLike
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<(int Likes, int Dislikes, bool? UserVote)> GetLikeInfoAsync(string videoId, string? userEmail)
    {
        var likes = await _db.VideoLikes.CountAsync(l => l.VideoId == videoId && l.IsLike);
        var dislikes = await _db.VideoLikes.CountAsync(l => l.VideoId == videoId && !l.IsLike);

        bool? userVote = null;
        if (!string.IsNullOrEmpty(userEmail))
        {
            var existing = await _db.VideoLikes
                .FirstOrDefaultAsync(l => l.VideoId == videoId && l.UserEmail == userEmail);
            userVote = existing?.IsLike;
        }

        return (likes, dislikes, userVote);
    }

    public async Task<Dictionary<string, (int Likes, int Dislikes)>> GetAllLikeCountsAsync()
    {
        return await _db.VideoLikes
            .GroupBy(l => l.VideoId)
            .ToDictionaryAsync(
                g => g.Key,
                g => (g.Count(l => l.IsLike), g.Count(l => !l.IsLike)));
    }

    // --- Comments ---

    public async Task<List<VideoComment>> GetCommentsAsync(string videoId)
    {
        return await _db.VideoComments
            .Where(c => c.VideoId == videoId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task AddCommentAsync(string videoId, string userEmail, string userName, string content)
    {
        _db.VideoComments.Add(new VideoComment
        {
            VideoId = videoId,
            UserEmail = userEmail,
            UserName = userName,
            Content = content.Trim()
        });
        await _db.SaveChangesAsync();
    }

    public async Task DeleteCommentAsync(int commentId, string? userEmail, bool isAdmin)
    {
        var comment = await _db.VideoComments.FindAsync(commentId);
        if (comment == null) return;

        if (!isAdmin && !comment.UserEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
            return;

        _db.VideoComments.Remove(comment);
        await _db.SaveChangesAsync();
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        _ => "application/octet-stream"
    };
}
