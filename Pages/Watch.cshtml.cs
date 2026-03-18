using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniTube.Models;
using MiniTube.Services;

namespace MiniTube.Pages;

public class WatchModel : PageModel
{
    private readonly VideoService _videoService;

    public WatchModel(VideoService videoService)
    {
        _videoService = videoService;
    }

    public VideoMetadata? Video { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
    public List<VideoMetadata> OtherVideos { get; set; } = new();

    public IActionResult OnGet(string id)
    {
        if (string.IsNullOrEmpty(id))
            return RedirectToPage("/Index");

        Video = _videoService.GetById(id);

        if (Video == null)
            return RedirectToPage("/Index");

        VideoUrl = $"/videos/{Video.FileName}";

        // Load other videos for sidebar (exclude current video)
        var allVideos = _videoService.GetAll();
        OtherVideos = allVideos
            .Where(v => v.Id != id)
            .OrderByDescending(v => v.UploadedAt)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        await _videoService.DeleteVideoAsync(id);
        return RedirectToPage("/Index");
    }
}
