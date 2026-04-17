namespace MiniTube.Services;

public interface ILikeService
{
    Task ToggleLikeAsync(string videoId, string userEmail, bool isLike);
    Task<(int Likes, int Dislikes, bool? UserVote)> GetLikeInfoAsync(string videoId, string? userEmail);
    Task<Dictionary<string, (int Likes, int Dislikes)>> GetAllLikeCountsAsync();
}
