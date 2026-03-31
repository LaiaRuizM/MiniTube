using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniTube.Models;
using MiniTube.Services;

namespace MiniTube.Pages;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly VideoService _videoService;

    public ProfileModel(VideoService videoService)
    {
        _videoService = videoService;
    }

    public List<VideoMetadata> MyVideos { get; set; } = new();
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public int TotalVideos { get; set; }

    public async Task OnGetAsync()
    {
        UserName = User.Identity?.Name ?? "Unknown";
        UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? "Unknown";
        IsAdmin = User.HasClaim("IsAdmin", "true");

        var all = await _videoService.GetAllAsync();

        if (IsAdmin)
        {
            MyVideos = all.OrderByDescending(v => v.UploadedAt).ToList();
        }
        else
        {
            MyVideos = all
                .Where(v => v.OwnerEmail != null &&
                       v.OwnerEmail.Equals(UserEmail, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.UploadedAt)
                .ToList();
        }

        TotalVideos = MyVideos.Count;
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.HasClaim("IsAdmin", "true");
        if (!await _videoService.CanUserEditAsync(id, email, isAdmin))
            return Forbid();

        await _videoService.DeleteVideoAsync(id);
        return RedirectToPage();
    }
}
