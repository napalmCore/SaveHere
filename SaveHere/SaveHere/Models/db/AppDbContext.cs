using Microsoft.EntityFrameworkCore;

namespace SaveHere.Models.db
{
  public class AppDbContext : DbContext
  {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<FileDownloadQueueItem> FileDownloadQueueItems { get; set; }
  }
}
