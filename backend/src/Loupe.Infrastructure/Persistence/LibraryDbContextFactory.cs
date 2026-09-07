using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loupe.Infrastructure.Persistence;

public sealed class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<LibraryDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Library") ?? "Host=localhost;Database=loupe;Username=loupe")
        .Options);
}
