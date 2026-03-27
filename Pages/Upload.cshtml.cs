using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniTube.Models;
using MiniTube.Services;

namespace MiniTube.Pages;

[Authorize]
[RequestSizeLimit(500 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 500 * 1024 * 1024)]
public class UploadModel : PageModel
{
    private readonly VideoService _videoService;
    private const long MaxFileSize = 500 * 1024 * 1024;

    public UploadModel(VideoService videoService)
    {
        _videoService = videoService;
    }

    [BindProperty]
    public UploadForm Form { get; set; } = new();

    public string[] Categories => VideoService.Categories;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Form.VideoFile!.Length > MaxFileSize)
        {
            ModelState.AddModelError("Form.VideoFile", "File size cannot exceed 500 MB.");
            return Page();
        }

        var allowedExtensions = new[] { ".mp4", ".webm", ".mov" };
        var extension = Path.GetExtension(Form.VideoFile.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            ModelState.AddModelError("Form.VideoFile", "Only .mp4, .webm, and .mov files are allowed.");
            return Page();
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var name = User.FindFirstValue(ClaimTypes.Name);
        await _videoService.SaveVideoAsync(Form, email, name);

        return RedirectToPage("/Index");
    }
}
