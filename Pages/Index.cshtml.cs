using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniTube.Models;
using MiniTube.Services;

namespace MiniTube.Pages;

public class IndexModel : PageModel
{
    private readonly IVideoService _videoService;
    private readonly ILikeService _likeService;

    public IndexModel(IVideoService videoService, ILikeService likeService)
    {
        _videoService = videoService;
        _likeService = likeService;
    }

    public List<VideoMetadata> Videos { get; set; } = new();
    public bool IsAdmin { get; set; }
    public Dictionary<string, (int Likes, int Dislikes)> LikeCounts { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    public List<string> Categories => VideoService.Categories.ToList();

    private const int PageSize = 9;

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }

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

        var ordered = filtered.OrderByDescending(v => v.UploadedAt).ToList();
        TotalPages = (int)Math.Ceiling(ordered.Count / (double)PageSize);
        CurrentPage = Math.Max(1, Math.Min(CurrentPage, TotalPages == 0 ? 1 : TotalPages));
        Videos = ordered.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

        IsAdmin = User.HasClaim("IsAdmin", "true");
        LikeCounts = await _likeService.GetAllLikeCountsAsync();
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
        TempData["Success"] = "Video deleted.";
        return RedirectToPage();
    }
}
