using Loupe.Domain.Sessions;
using Loupe.Domain.Photographs;
using Loupe.Domain.Operations;
using Loupe.Domain.Deletions;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationSession> Sessions => Set<ApplicationSession>();
    public DbSet<Photograph> Photographs => Set<Photograph>();
    public DbSet<OperationReceipt> OperationReceipts => Set<OperationReceipt>();
    public DbSet<DeletionOperation> Deletions => Set<DeletionOperation>();
    public DbSet<BackgroundOperation> BackgroundOperations => Set<BackgroundOperation>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSession>().ToTable("sessions").HasKey(session => session.Id);
        modelBuilder.Entity<Photograph>().ToTable("photographs").HasKey(photograph => photograph.Id);
        modelBuilder.Entity<Photograph>().HasIndex(photograph => new { photograph.OwnerId, photograph.CreatedAt, photograph.Id });
        modelBuilder.Entity<Photograph>().ComplexProperty(photograph => photograph.Exif).ToJson();
        modelBuilder.Entity<Photograph>().ComplexProperty(photograph => photograph.Brief).ToJson();
        modelBuilder.Entity<Photograph>().Property(photograph => photograph.CritiqueJson).HasColumnType("jsonb");
        modelBuilder.Entity<Photograph>().Property(photograph => photograph.Revision).HasDefaultValue(1L).IsConcurrencyToken();
        modelBuilder.Entity<OperationReceipt>().ToTable("operation_receipts").HasKey(receipt => new { receipt.OwnerId, receipt.OperationType, receipt.Key });
        modelBuilder.Entity<OperationReceipt>().Property(receipt => receipt.OwnerId).HasMaxLength(64);
        modelBuilder.Entity<OperationReceipt>().Property(receipt => receipt.OperationType).HasMaxLength(64);
        modelBuilder.Entity<OperationReceipt>().Property(receipt => receipt.Key).HasMaxLength(128);
        modelBuilder.Entity<OperationReceipt>().HasIndex(receipt => receipt.CreatedAt);
        modelBuilder.Entity<DeletionOperation>().ToTable("deletions", "journal").HasKey(deletion => deletion.Id);
        modelBuilder.Entity<DeletionOperation>().Property(deletion => deletion.OwnerId).HasMaxLength(64);
        modelBuilder.Entity<DeletionOperation>().Property(deletion => deletion.ResourceType).HasMaxLength(64);
        modelBuilder.Entity<DeletionOperation>().HasIndex(deletion => new { deletion.OwnerId, deletion.ResourceType, deletion.ResourceId }).IsUnique();
        modelBuilder.Entity<DeletionOperation>().HasIndex(deletion => new { deletion.CompletedAt, deletion.LastAttemptAt, deletion.DeletedAt });
        modelBuilder.Entity<BackgroundOperation>().ToTable("background_operations").HasKey(operation => operation.Id);
        modelBuilder.Entity<BackgroundOperation>().Property(operation => operation.OwnerId).HasMaxLength(64);
        modelBuilder.Entity<BackgroundOperation>().Property(operation => operation.Type).HasConversion<string>().HasMaxLength(64);
        modelBuilder.Entity<BackgroundOperation>().Property(operation => operation.Mode).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<BackgroundOperation>().Property(operation => operation.Status).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<BackgroundOperation>().Property(operation => operation.InputJson).HasColumnType("jsonb");
        modelBuilder.Entity<BackgroundOperation>().HasIndex(operation => new { operation.OwnerId, operation.Type, operation.ResourceId });
        modelBuilder.Entity<BackgroundOperation>().HasIndex(operation => new { operation.Status, operation.CreatedAt });
    }
}
