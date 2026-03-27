using System.ComponentModel.DataAnnotations;

namespace MiniTube.Models;

public class VideoMetadata
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public long FileSizeBytes { get; set; }

    [MaxLength(260)]
    public string? ThumbnailFileName { get; set; }

    // Blob Storage URLs (used when deployed to Azure)
    [MaxLength(500)]
    public string? BlobUrl { get; set; }

    [MaxLength(500)]
    public string? ThumbnailBlobUrl { get; set; }

    // Ownership (who uploaded this video)
    [MaxLength(100)]
    public string? OwnerEmail { get; set; }

    [MaxLength(200)]
    public string? OwnerName { get; set; }
}
