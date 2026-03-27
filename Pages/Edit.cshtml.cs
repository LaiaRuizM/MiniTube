using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniTube.Models;
using MiniTube.Services;

namespace MiniTube.Pages;

[Authorize]
public class EditModel : PageModel
{
    private readonly VideoService _videoService;
    private const long MaxFileSize = 500 * 1024 * 1024;

    public EditModel(VideoService videoService)
    {
        _videoService = videoService;
    }

    [BindProperty]
    public EditForm Form { get; set; } = new();

    public VideoMetadata? Video { get; set; }

    public string[] Categories => VideoService.Categories;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var video = await _videoService.GetByIdAsync(id);
        if (video == null)
            return RedirectToPage("/Index");

        // Check ownership
        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.HasClaim("IsAdmin", "true");
        if (!await _videoService.CanUserEditAsync(id, email, isAdmin))
            return RedirectToPage("/Index");

        Video = video;
        Form.Id = video.Id;
        Form.Title = video.Title;
        Form.Description = video.Description;
        Form.Category = video.Category;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Check ownership
        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.HasClaim("IsAdmin", "true");
        if (!await _videoService.CanUserEditAsync(Form.Id, email, isAdmin))
            return RedirectToPage("/Index");

        if (Form.VideoFile != null && Form.VideoFile.Length > 0)
        {
            if (Form.VideoFile.Length > MaxFileSize)
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

            await _videoService.UpdateVideoFileAsync(Form.Id, Form.VideoFile);
        }

        await _videoService.UpdateMetadataAsync(Form.Id, Form.Title, Form.Description, Form.Category);

        return RedirectToPage("/Watch", new { id = Form.Id });
    }
}
