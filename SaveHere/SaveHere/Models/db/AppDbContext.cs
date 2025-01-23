using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaveHere.Models.SaveHere.Models;

namespace SaveHere.Models.db
{
  public class AppDbContext : IdentityDbContext<ApplicationUser>
  {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<FileDownloadQueueItem> FileDownloadQueueItems { get; set; }
    public DbSet<YoutubeDownloadQueueItem> YoutubeDownloadQueueItems { get; set; }
  }
}
