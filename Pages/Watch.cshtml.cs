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

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            return RedirectToPage("/Index");

        Video = await _videoService.GetByIdAsync(id);

        if (Video == null)
            return RedirectToPage("/Index");

        // Use SAS URL for private blob, or fall back to local path
        VideoUrl = _videoService.GetBlobSasUrl(Video.FileName)
                   ?? $"/videos/{Video.FileName}";

        var allVideos = await _videoService.GetAllAsync();
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
