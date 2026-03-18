using System.Text.Json;
using MiniTube.Models;

namespace MiniTube.Services;

public class VideoService
{
    private readonly string _videoFolder;
    private readonly string _metadataPath;
    private readonly string _thumbnailFolder;
    private readonly object _lock = new();

    private static readonly string[] AllowedExtensions = { ".mp4", ".webm", ".mov" };
    public static readonly string[] Categories = { "Education", "Entertainment", "Gaming", "Music", "Tech", "Other" };

    public VideoService(IWebHostEnvironment env)
    {
        var storageRoot = Path.Combine(env.ContentRootPath, "Storage");
        _videoFolder = Path.Combine(storageRoot, "videos");
        _metadataPath = Path.Combine(storageRoot, "metadata.json");
        _thumbnailFolder = Path.Combine(_videoFolder, "thumbnails");

        Directory.CreateDirectory(_videoFolder);
        Directory.CreateDirectory(_thumbnailFolder);
    }

    public List<VideoMetadata> GetAll()
    {
        lock (_lock)
        {
            if (!File.Exists(_metadataPath))
                return new List<VideoMetadata>();

            var json = File.ReadAllText(_metadataPath);
            return JsonSerializer.Deserialize<List<VideoMetadata>>(json) ?? new List<VideoMetadata>();
        }
    }

    public VideoMetadata? GetById(string id)
    {
        return GetAll().FirstOrDefault(v => v.Id == id);
    }

    public async Task<VideoMetadata> SaveVideoAsync(UploadForm form)
    {
        var file = form.VideoFile!;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

        var metadata = new VideoMetadata
        {
            Title = form.Title.Trim(),
            Description = form.Description?.Trim() ?? string.Empty,
            Category = form.Category,
            FileName = $"{Guid.NewGuid()}{extension}",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = file.Length
        };

        // Save the video file to disk
        var filePath = Path.Combine(_videoFolder, metadata.FileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Generate thumbnail from video
        metadata.ThumbnailFileName = await GenerateThumbnailAsync(filePath, metadata.Id);

        // Append metadata to JSON
        lock (_lock)
        {
            var videos = GetAllUnsafe();
            videos.Add(metadata);
            var json = JsonSerializer.Serialize(videos, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_metadataPath, json);
        }

        return metadata;
    }

    public Task UpdateMetadataAsync(string id, string title, string? description, string category)
    {
        lock (_lock)
        {
            var videos = GetAllUnsafe();
            var video = videos.FirstOrDefault(v => v.Id == id);
            if (video != null)
            {
                video.Title = title;
                video.Description = description ?? string.Empty;
                video.Category = category;
                var json = JsonSerializer.Serialize(videos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataPath, json);
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteVideoAsync(string id)
    {
        lock (_lock)
        {
            var videos = GetAllUnsafe();
            var video = videos.FirstOrDefault(v => v.Id == id);
            if (video != null)
            {
                var videoPath = Path.Combine(_videoFolder, video.FileName);
                if (File.Exists(videoPath)) File.Delete(videoPath);

                if (!string.IsNullOrEmpty(video.ThumbnailFileName))
                {
                    var thumbPath = Path.Combine(_thumbnailFolder, video.ThumbnailFileName);
                    if (File.Exists(thumbPath)) File.Delete(thumbPath);
                }

                videos.Remove(video);
                var json = JsonSerializer.Serialize(videos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataPath, json);
            }
        }
        return Task.CompletedTask;
    }

    public async Task UpdateVideoFileAsync(string id, IFormFile newVideoFile)
    {
        var extension = Path.GetExtension(newVideoFile.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");

        string newFileName;
        lock (_lock)
        {
            var videos = GetAllUnsafe();
            var video = videos.FirstOrDefault(v => v.Id == id);
            if (video == null)
                return;

            // Delete old video file
            var oldVideoPath = Path.Combine(_videoFolder, video.FileName);
            if (File.Exists(oldVideoPath)) File.Delete(oldVideoPath);

            // Delete old thumbnail
            if (!string.IsNullOrEmpty(video.ThumbnailFileName))
            {
                var oldThumbPath = Path.Combine(_thumbnailFolder, video.ThumbnailFileName);
                if (File.Exists(oldThumbPath)) File.Delete(oldThumbPath);
            }

            // Generate new filename
            newFileName = $"{Guid.NewGuid()}{extension}";
            video.FileName = newFileName;
            video.FileSizeBytes = newVideoFile.Length;
            video.ThumbnailFileName = null;

            var json = JsonSerializer.Serialize(videos, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_metadataPath, json);
        }

        // Save new video file outside of lock
        var newFilePath = Path.Combine(_videoFolder, newFileName);
        using (var stream = new FileStream(newFilePath, FileMode.Create))
        {
            await newVideoFile.CopyToAsync(stream);
        }

        // Generate new thumbnail
        var thumbnailFileName = await GenerateThumbnailAsync(newFilePath, id);

        // Update metadata with thumbnail
        lock (_lock)
        {
            var videos = GetAllUnsafe();
            var video = videos.FirstOrDefault(v => v.Id == id);
            if (video != null)
            {
                video.ThumbnailFileName = thumbnailFileName;
                var json = JsonSerializer.Serialize(videos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataPath, json);
            }
        }
    }


    private async Task<string?> GenerateThumbnailAsync(string videoFilePath, string videoId)
    {
        var outputFileName = $"{videoId}.jpg";
        var outputPath = Path.Combine(_thumbnailFolder, outputFileName);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -ss 2 -i \"{videoFilePath}\" -vframes 1 -q:v 2 -update 1 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return null;
            await process.WaitForExitAsync();
            return process.ExitCode == 0 && File.Exists(outputPath) ? outputFileName : null;
        }
        catch
        {
            return null;
        }
    }

    public string GetVideoFilePath(string fileName)
    {
        return Path.Combine(_videoFolder, fileName);
    }

    private List<VideoMetadata> GetAllUnsafe()
    {
        if (!File.Exists(_metadataPath))
            return new List<VideoMetadata>();

        var json = File.ReadAllText(_metadataPath);
        return JsonSerializer.Deserialize<List<VideoMetadata>>(json) ?? new List<VideoMetadata>();
    }
}
