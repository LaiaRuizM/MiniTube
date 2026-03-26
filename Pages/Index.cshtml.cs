using Microsoft.AspNetCore.Mvc;
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

    public async Task OnGetAsync()
    {
        var all = await _videoService.GetAllAsync();
        Videos = all.OrderByDescending(v => v.UploadedAt).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        await _videoService.DeleteVideoAsync(id);
        return RedirectToPage();
    }
}
