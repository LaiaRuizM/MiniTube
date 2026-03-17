using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniTube.Models;
using MiniTube.Services;

namespace MiniTube.Pages;

public class IndexModel : PageModel
{
    private readonly VideoService _videoService;

    public IndexModel(VideoService videoService)
    {
        _videoService = videoService;
    }

    public List<VideoMetadata> Videos { get; set; } = new();

    public void OnGet()
    {
        Videos = _videoService.GetAll()
            .OrderByDescending(v => v.UploadedAt)
            .ToList();
    }
}
