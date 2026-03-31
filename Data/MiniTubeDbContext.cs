using Microsoft.EntityFrameworkCore;
using MiniTube.Models;

namespace MiniTube.Data;

public class MiniTubeDbContext : DbContext
{
    public MiniTubeDbContext(DbContextOptions<MiniTubeDbContext> options) : base(options) { }

    public DbSet<VideoMetadata> Videos => Set<VideoMetadata>();
    public DbSet<VideoLike> VideoLikes => Set<VideoLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VideoMetadata>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Title).IsRequired().HasMaxLength(100);
            entity.Property(v => v.Description).HasMaxLength(500);
            entity.Property(v => v.Category).IsRequired().HasMaxLength(50);
            entity.Property(v => v.FileName).IsRequired().HasMaxLength(260);
            entity.Property(v => v.ThumbnailFileName).HasMaxLength(260);
            entity.Property(v => v.BlobUrl).HasMaxLength(500);
            entity.Property(v => v.ThumbnailBlobUrl).HasMaxLength(500);
            entity.Property(v => v.OwnerEmail).HasMaxLength(100);
            entity.Property(v => v.OwnerName).HasMaxLength(200);
        });

        modelBuilder.Entity<VideoLike>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.VideoId).IsRequired().HasMaxLength(100);
            entity.Property(l => l.UserEmail).IsRequired().HasMaxLength(100);
            // One like/dislike per user per video
            entity.HasIndex(l => new { l.VideoId, l.UserEmail }).IsUnique();
        });
    }
}
