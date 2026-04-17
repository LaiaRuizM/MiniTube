using MiniTube.Models;

namespace MiniTube.Services;

public interface IVideoService
{
    string? GetBlobSasUrl(string blobName);
    Task<List<VideoMetadata>> GetAllAsync();
    Task<VideoMetadata?> GetByIdAsync(string id);
    Task IncrementViewCountAsync(string id);
    Task<bool> CanUserEditAsync(string videoId, string? userEmail, bool isAdmin);
    Task<VideoMetadata> SaveVideoAsync(UploadForm form, string? ownerEmail, string? ownerName);
    Task UpdateMetadataAsync(string id, string title, string? description, string category);
    Task DeleteVideoAsync(string id);
    Task UpdateVideoFileAsync(string id, IFormFile newVideoFile);
    Task<string?> GetProfilePictureUrlAsync(string userEmail);
    Task SaveProfilePictureAsync(string userEmail, IFormFile file);
    Task<List<VideoMetadata>> GetRelatedVideosAsync(string videoId, int count = 5);
}
