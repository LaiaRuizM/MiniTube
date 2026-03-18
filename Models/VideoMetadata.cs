namespace MiniTube.Models;

public class VideoMetadata
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long FileSizeBytes { get; set; }
    public string? ThumbnailFileName { get; set; }
}
