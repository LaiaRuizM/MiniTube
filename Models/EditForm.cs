using System.ComponentModel.DataAnnotations;

namespace MiniTube.Models;

public class EditForm
{
    public string Id { get; set; } = string.Empty;
    [Required][MaxLength(100)] public string Title { get; set; } = string.Empty;
    [MaxLength(500)]            public string? Description { get; set; }
    [Required]                  public string Category { get; set; } = string.Empty;
    public IFormFile? VideoFile { get; set; }
}
