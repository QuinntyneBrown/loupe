using Loupe.Domain.Sessions;
using Loupe.Domain.Photographs;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationSession> Sessions => Set<ApplicationSession>();
    public DbSet<Photograph> Photographs => Set<Photograph>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSession>().ToTable("sessions").HasKey(session => session.Id);
        modelBuilder.Entity<Photograph>().ToTable("photographs").HasKey(photograph => photograph.Id);
        modelBuilder.Entity<Photograph>().HasIndex(photograph => new { photograph.OwnerId, photograph.CreatedAt, photograph.Id });
    }
}
