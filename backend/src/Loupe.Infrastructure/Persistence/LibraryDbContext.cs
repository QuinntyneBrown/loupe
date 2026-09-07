using Loupe.Domain.Sessions;
using Loupe.Domain.Photographs;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationSession> Sessions => Set<ApplicationSession>();
    public DbSet<Photograph> Photographs => Set<Photograph>();
    public DbSet<OperationReceipt> OperationReceipts => Set<OperationReceipt>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSession>().ToTable("sessions").HasKey(session => session.Id);
        modelBuilder.Entity<Photograph>().ToTable("photographs").HasKey(photograph => photograph.Id);
        modelBuilder.Entity<Photograph>().HasIndex(photograph => new { photograph.OwnerId, photograph.CreatedAt, photograph.Id });
        modelBuilder.Entity<Photograph>().ComplexProperty(photograph => photograph.Exif).ToJson();
        modelBuilder.Entity<Photograph>().ComplexProperty(photograph => photograph.Brief).ToJson();
        modelBuilder.Entity<Photograph>().Property(photograph => photograph.Revision).HasDefaultValue(1L).IsConcurrencyToken();
        modelBuilder.Entity<OperationReceipt>().ToTable("operation_receipts").HasKey(receipt => new { receipt.OwnerId, receipt.OperationType, receipt.Key });
        modelBuilder.Entity<OperationReceipt>().Property(receipt => receipt.OwnerId).HasMaxLength(64);
        modelBuilder.Entity<OperationReceipt>().Property(receipt => receipt.OperationType).HasMaxLength(64);
        modelBuilder.Entity<OperationReceipt>().Property(receipt => receipt.Key).HasMaxLength(128);
        modelBuilder.Entity<OperationReceipt>().HasIndex(receipt => receipt.CreatedAt);
    }
}
