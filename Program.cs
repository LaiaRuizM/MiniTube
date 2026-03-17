using Microsoft.Extensions.FileProviders;
using MiniTube.Services;

var builder = WebApplication.CreateBuilder(args);

// Allow large file uploads (up to 500 MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<VideoService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Serve uploaded videos from Storage/videos as /videos/{filename}
var videoPath = Path.Combine(app.Environment.ContentRootPath, "Storage", "videos");
Directory.CreateDirectory(videoPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(videoPath),
    RequestPath = "/videos"
});

app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
