using Microsoft.EntityFrameworkCore;
using MiniTube.Data;
using MiniTube.Models;

namespace MiniTube.Services;

public class CommentService : ICommentService
{
    private readonly MiniTubeDbContext _db;

    public CommentService(MiniTubeDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, int>> GetAllCommentCountsAsync()
    {
        var comments = await _db.VideoComments.ToListAsync();
        return comments
            .GroupBy(c => c.VideoId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<List<VideoComment>> GetCommentsAsync(string videoId)
    {
        return await _db.VideoComments
            .Where(c => c.VideoId == videoId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task AddCommentAsync(string videoId, string userEmail, string userName, string content)
    {
        _db.VideoComments.Add(new VideoComment
        {
            VideoId = videoId,
            UserEmail = userEmail,
            UserName = userName,
            Content = content.Trim()
        });
        await _db.SaveChangesAsync();
    }

    public async Task DeleteCommentAsync(int commentId, string? userEmail, bool isAdmin)
    {
        var comment = await _db.VideoComments.FindAsync(commentId);
        if (comment == null) return;

        if (!isAdmin && !comment.UserEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
            return;

        _db.VideoComments.Remove(comment);
        await _db.SaveChangesAsync();
    }
}
