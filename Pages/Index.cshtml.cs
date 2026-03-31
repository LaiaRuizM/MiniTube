using System.Security.Claims;
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
    public bool IsAdmin { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    public List<string> Categories => VideoService.Categories.ToList();

    public async Task OnGetAsync()
    {
        var all = await _videoService.GetAllAsync();
        var filtered = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(Search))
            filtered = filtered.Where(v =>
                v.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                (v.Description ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(Category))
            filtered = filtered.Where(v =>
                v.Category.Equals(Category, StringComparison.OrdinalIgnoreCase));

        Videos = filtered.OrderByDescending(v => v.UploadedAt).ToList();
        IsAdmin = User.HasClaim("IsAdmin", "true");
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.HasClaim("IsAdmin", "true");
        if (!await _videoService.CanUserEditAsync(id, email, isAdmin))
            return Forbid();

        await _videoService.DeleteVideoAsync(id);
        return RedirectToPage();
    }
}
