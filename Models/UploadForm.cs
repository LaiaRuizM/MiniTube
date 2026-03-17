using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MiniTube.Models;

public class UploadForm
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please select a category.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a video file.")]
    public IFormFile? VideoFile { get; set; }
}
