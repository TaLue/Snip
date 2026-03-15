using Microsoft.EntityFrameworkCore;
using Snip.Models;

namespace Snip.Data;

public class SnipDbContext(DbContextOptions<SnipDbContext> options) : DbContext(options)
{
    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();
    public DbSet<ClickLog> ClickLogs => Set<ClickLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortLink>()
            .HasIndex(s => s.Slug)
            .IsUnique();
    }
}
