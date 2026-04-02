using System.ComponentModel.DataAnnotations;

namespace MiniTube.Models;

public class VideoComment
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string VideoId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
