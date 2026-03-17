using System.Text.Json;
using MiniTube.Models;

namespace MiniTube.Services;

public class VideoService
{
    private readonly string _videoFolder;
    private readonly string _metadataPath;
    private readonly object _lock = new();

    private static readonly string[] AllowedExtensions = { ".mp4", ".webm", ".mov" };
    public static readonly string[] Categories = { "Education", "Entertainment", "Gaming", "Music", "Tech", "Other" };

    public VideoService(IWebHostEnvironment env)
    {
        var storageRoot = Path.Combine(env.ContentRootPath, "Storage");
        _videoFolder = Path.Combine(storageRoot, "videos");
        _metadataPath = Path.Combine(storageRoot, "metadata.json");

        Directory.CreateDirectory(_videoFolder);
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
