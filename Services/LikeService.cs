using Microsoft.EntityFrameworkCore;
using MiniTube.Data;
using MiniTube.Models;

namespace MiniTube.Services;

public class LikeService : ILikeService
{
    private readonly MiniTubeDbContext _db;

    public LikeService(MiniTubeDbContext db)
    {
        _db = db;
    }

    public async Task ToggleLikeAsync(string videoId, string userEmail, bool isLike)
    {
        var existing = await _db.VideoLikes
            .FirstOrDefaultAsync(l => l.VideoId == videoId && l.UserEmail == userEmail);

        if (existing != null)
        {
            if (existing.IsLike == isLike)
            {
                // Same button clicked again → remove the vote
                _db.VideoLikes.Remove(existing);
            }
            else
            {
                // Switch from like to dislike or vice versa
                existing.IsLike = isLike;
            }
        }
        else
        {
            // New vote
            _db.VideoLikes.Add(new VideoLike
            {
                VideoId = videoId,
                UserEmail = userEmail,
                IsLike = isLike
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<(int Likes, int Dislikes, bool? UserVote)> GetLikeInfoAsync(string videoId, string? userEmail)
    {
        var likes = await _db.VideoLikes.CountAsync(l => l.VideoId == videoId && l.IsLike);
        var dislikes = await _db.VideoLikes.CountAsync(l => l.VideoId == videoId && !l.IsLike);

        bool? userVote = null;
        if (!string.IsNullOrEmpty(userEmail))
        {
            var existing = await _db.VideoLikes
                .FirstOrDefaultAsync(l => l.VideoId == videoId && l.UserEmail == userEmail);
            userVote = existing?.IsLike;
        }

        return (likes, dislikes, userVote);
    }

    public async Task<Dictionary<string, (int Likes, int Dislikes)>> GetAllLikeCountsAsync()
    {
        var likes = await _db.VideoLikes.ToListAsync();
        return likes
            .GroupBy(l => l.VideoId)
            .ToDictionary(
                g => g.Key,
                g => (g.Count(l => l.IsLike), g.Count(l => !l.IsLike)));
    }
}
