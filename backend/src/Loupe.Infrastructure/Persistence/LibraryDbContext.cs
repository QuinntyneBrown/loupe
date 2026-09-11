using Loupe.Domain.Sessions;
using Loupe.Domain.Photographs;
using Loupe.Domain.Operations;
using Loupe.Domain.Deletions;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;
using Loupe.Domain.Boards;

namespace Loupe.Infrastructure.Persistence;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Loupe.Domain.Users.User> Users => Set<Loupe.Domain.Users.User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardReference> BoardReferences => Set<BoardReference>();
    public DbSet<ApplicationSession> Sessions => Set<ApplicationSession>();
    public DbSet<Photograph> Photographs => Set<Photograph>();
    public DbSet<Reference> References => Set<Reference>();
    public DbSet<ReferenceDraft> ReferenceDrafts => Set<ReferenceDraft>();
    public DbSet<OperationReceipt> OperationReceipts => Set<OperationReceipt>();
    public DbSet<DeletionOperation> Deletions => Set<DeletionOperation>();
    public DbSet<BackgroundOperation> BackgroundOperations => Set<BackgroundOperation>();
    public DbSet<AnalysisDispatchCursor> AnalysisDispatchCursors => Set<AnalysisDispatchCursor>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Loupe.Domain.Users.User>().ToTable("users").HasKey(u => u.Id);
        modelBuilder.Entity<Loupe.Domain.Users.User>().HasIndex(u => u.NormalizedEmail).IsUnique();
        modelBuilder.Entity<Loupe.Domain.Users.User>().Property(u => u.NormalizedEmail).HasMaxLength(254);
        modelBuilder.Entity<Loupe.Domain.Users.User>().Property(u => u.PasswordVersion).HasDefaultValue("");
        modelBuilder.Entity<ReferenceDraft>().ToTable("reference_drafts").HasKey(draft => draft.Id);
        modelBuilder.Entity<ReferenceDraft>().HasIndex(draft => new { draft.OwnerId, draft.ExpiresAt });
        modelBuilder.Entity<ReferenceDraft>().Property(draft => draft.Revision).IsConcurrencyToken();
        modelBuilder.Entity<ReferenceDraft>().HasOne(draft => draft.ImportOperation).WithMany()
            .HasForeignKey(draft => draft.ImportOperationId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ReferenceTag>().ToTable("reference_tags").HasKey(tag => new { tag.ReferenceId, tag.NormalizedName });
        modelBuilder.Entity<ReferenceTag>().HasIndex(tag => new { tag.OwnerId, tag.NormalizedName });
        modelBuilder.Entity<ReferenceTag>().HasOne<Reference>().WithMany(reference => reference.Tags)
            .HasForeignKey(tag => new { tag.ReferenceId, tag.OwnerId }).HasPrincipalKey(reference => new { reference.Id, reference.OwnerId }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Board>().ToTable("boards").HasKey(board => board.Id);
        modelBuilder.Entity<Board>().HasAlternateKey(board => new { board.Id, board.OwnerId });
        modelBuilder.Entity<Board>().HasIndex(board => new { board.OwnerId, board.NormalizedName }).IsUnique();
        modelBuilder.Entity<Board>().Property(board => board.Revision).IsConcurrencyToken();
        modelBuilder.Entity<Reference>().HasAlternateKey(reference => new { reference.Id, reference.OwnerId });
        modelBuilder.Entity<BoardReference>().ToTable("board_references").HasKey(item => new { item.BoardId, item.ReferenceId });
        modelBuilder.Entity<BoardReference>().HasOne<Board>().WithMany(board => board.References)
            .HasForeignKey(item => new { item.BoardId, item.OwnerId }).HasPrincipalKey(board => new { board.Id, board.OwnerId }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<BoardReference>().HasOne<Reference>().WithMany(reference => reference.Boards)
            .HasForeignKey(item => new { item.ReferenceId, item.OwnerId }).HasPrincipalKey(reference => new { reference.Id, reference.OwnerId }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Reference>().ToTable("references").HasKey(reference => reference.Id);
        modelBuilder.Entity<Reference>().HasIndex(reference => new { reference.OwnerId, reference.CreatedAt, reference.Id });
        modelBuilder.Entity<Reference>().Property(reference => reference.Revision).HasDefaultValue(1L).IsConcurrencyToken();
        modelBuilder.Entity<Reference>().Property(reference => reference.ImageRevision).HasDefaultValue(1L);
        modelBuilder.Entity<Reference>().Property(reference => reference.SuggestionsJson).HasColumnType("jsonb");
        modelBuilder.Entity<Reference>().Property(reference => reference.SourceImportJson).HasColumnType("jsonb");
        modelBuilder.Entity<ReferenceDraft>().Property(reference => reference.SourceImportJson).HasColumnType("jsonb");
        modelBuilder.Entity<Reference>().Property(reference => reference.SuggestionUndoJson).HasColumnType("jsonb");
        modelBuilder.Entity<Reference>().Property<string>("SourceHash").HasMaxLength(32)
            .HasComputedColumnSql("md5(loupe_normalize_source(\"SourceUrl\"))", stored: true);
        modelBuilder.Entity<Reference>().HasIndex("OwnerId", "SourceHash");
        modelBuilder.Entity<AnalysisDispatchCursor>().ToTable("analysis_dispatch_cursor").HasKey(cursor => cursor.Id);
        modelBuilder.Entity<AnalysisDispatchCursor>().Property(cursor => cursor.OwnerId).HasMaxLength(64);
        modelBuilder.Entity<AnalysisDispatchCursor>().HasData(new AnalysisDispatchCursor());
        modelBuilder.Entity<ApplicationSession>().ToTable("sessions").HasKey(session => session.Id);
        modelBuilder.Entity<Photograph>().ToTable("photographs").HasKey(photograph => photograph.Id);
        modelBuilder.Entity<Photograph>().HasIndex(photograph => new { photograph.OwnerId, photograph.CreatedAt, photograph.Id });
        modelBuilder.Entity<Photograph>().ComplexProperty(photograph => photograph.Exif).ToJson();
        modelBuilder.Entity<Photograph>().ComplexProperty(photograph => photograph.Brief).ToJson();
        modelBuilder.Entity<Photograph>().Property(photograph => photograph.ArchivedDemoCritiqueJson).HasColumnType("jsonb");
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
        modelBuilder.Entity<BackgroundOperation>().Property(operation => operation.OutputJson).HasColumnType("jsonb");
        modelBuilder.Entity<BackgroundOperation>().HasIndex(operation => new { operation.OwnerId, operation.Type, operation.ResourceId });
        modelBuilder.Entity<BackgroundOperation>().HasIndex(operation => new { operation.Status, operation.CreatedAt });
    }
}
