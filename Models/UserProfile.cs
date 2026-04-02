using System.ComponentModel.DataAnnotations;

namespace MiniTube.Models;

public class UserProfile
{
    [Key]
    [MaxLength(100)]
    public string UserEmail { get; set; } = string.Empty;

    [MaxLength(260)]
    public string? PictureFileName { get; set; }
}
