using System.ComponentModel.DataAnnotations;

namespace MiniTube.Models;

public class VideoLike
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string VideoId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// true = like, false = dislike
    /// </summary>
    public bool IsLike { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
