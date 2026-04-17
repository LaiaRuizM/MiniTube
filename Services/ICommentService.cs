using MiniTube.Models;

namespace MiniTube.Services;

public interface ICommentService
{
    Task<Dictionary<string, int>> GetAllCommentCountsAsync();
    Task<List<VideoComment>> GetCommentsAsync(string videoId);
    Task AddCommentAsync(string videoId, string userEmail, string userName, string content);
    Task DeleteCommentAsync(int commentId, string? userEmail, bool isAdmin);
}
