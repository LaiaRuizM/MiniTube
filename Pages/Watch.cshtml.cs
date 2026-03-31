using System.Security.Claims;
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
    public bool CanEdit { get; set; }
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public bool? UserVote { get; set; } // true=liked, false=disliked, null=no vote

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            return RedirectToPage("/Index");

        Video = await _videoService.GetByIdAsync(id);

        if (Video == null)
            return RedirectToPage("/Index");

        VideoUrl = _videoService.GetBlobSasUrl(Video.FileName)
                   ?? $"/videos/{Video.FileName}";

        var allVideos = await _videoService.GetAllAsync();
        OtherVideos = allVideos
            .Where(v => v.Id != id)
            .OrderByDescending(v => v.UploadedAt)
            .ToList();

        // Check if current user can edit/delete this video
        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.HasClaim("IsAdmin", "true");
        CanEdit = User.Identity?.IsAuthenticated == true &&
                  (isAdmin || (Video.OwnerEmail != null &&
                   Video.OwnerEmail.Equals(email, StringComparison.OrdinalIgnoreCase)));

        // Get like info
        var likeInfo = await _videoService.GetLikeInfoAsync(id, email);
        LikeCount = likeInfo.Likes;
        DislikeCount = likeInfo.Dislikes;
        UserVote = likeInfo.UserVote;

        return Page();
    }

    public async Task<IActionResult> OnPostLikeAsync(string id)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var email = User.FindFirstValue(ClaimTypes.Email)!;
        await _videoService.ToggleLikeAsync(id, email, isLike: true);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDislikeAsync(string id)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var email = User.FindFirstValue(ClaimTypes.Email)!;
        await _videoService.ToggleLikeAsync(id, email, isLike: false);
        return RedirectToPage(new { id });
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
        return RedirectToPage("/Index");
    }
}
