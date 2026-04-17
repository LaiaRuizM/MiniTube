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
    private readonly IVideoService _videoService;
    private readonly ILikeService _likeService;
    private readonly ICommentService _commentService;

    public ProfileModel(IVideoService videoService, ILikeService likeService, ICommentService commentService)
    {
        _videoService = videoService;
        _likeService = likeService;
        _commentService = commentService;
    }

    public List<VideoMetadata> MyVideos { get; set; } = new();
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public int TotalVideos { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public Dictionary<string, (int Likes, int Dislikes)> LikeCounts { get; set; } = new();
    public Dictionary<string, int> CommentCounts { get; set; } = new();

    [BindProperty]
    public IFormFile? PictureFile { get; set; }

    public async Task OnGetAsync()
    {
        UserName = User.Identity?.Name ?? "Unknown";
        UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? "Unknown";
        IsAdmin = User.HasClaim("IsAdmin", "true");
        ProfilePictureUrl = await _videoService.GetProfilePictureUrlAsync(UserEmail);
        LikeCounts = await _likeService.GetAllLikeCountsAsync();
        CommentCounts = await _commentService.GetAllCommentCountsAsync();

        var all = await _videoService.GetAllAsync();

        MyVideos = IsAdmin
            ? all.OrderByDescending(v => v.UploadedAt).ToList()
            : all.Where(v => v.OwnerEmail != null &&
                       v.OwnerEmail.Equals(UserEmail, StringComparison.OrdinalIgnoreCase))
                 .OrderByDescending(v => v.UploadedAt).ToList();

        TotalVideos = MyVideos.Count;
    }

    public async Task<IActionResult> OnPostUploadPictureAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!;
        if (PictureFile != null && PictureFile.Length > 0)
        {
            await _videoService.SaveProfilePictureAsync(email, PictureFile);
            TempData["Success"] = "Profile picture updated!";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.HasClaim("IsAdmin", "true");
        if (!await _videoService.CanUserEditAsync(id, email, isAdmin))
            return Forbid();

        await _videoService.DeleteVideoAsync(id);
        TempData["Success"] = "Video deleted.";
        return RedirectToPage();
    }
}
