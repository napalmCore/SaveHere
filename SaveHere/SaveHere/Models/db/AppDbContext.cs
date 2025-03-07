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
    public DbSet<RegistrationSettings> RegistrationSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
      base.OnModelCreating(builder);

      // Ensure there's always exactly one RegistrationSettings row
      builder.Entity<RegistrationSettings>().HasData(
          new RegistrationSettings { Id = 1, IsRegistrationEnabled = false }
      );
    }
  }
}
