using Loupe.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationSession> Sessions => Set<ApplicationSession>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSession>().ToTable("sessions").HasKey(session => session.Id);
    }
}
