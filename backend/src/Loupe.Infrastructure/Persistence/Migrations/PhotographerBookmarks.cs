using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable enable

namespace Loupe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LibraryDbContext))]
    [Migration("20260911170045_PhotographerBookmarks")]
    public sealed class PhotographerBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photographers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PortfolioUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    SummaryProvenance = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    PortfolioHash = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, computedColumnSql: "md5(loupe_normalize_source(\"PortfolioUrl\"))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photographers", x => x.Id);
                    table.UniqueConstraint("AK_photographers_Id_OwnerId", x => new { x.Id, x.OwnerId });
                });

            migrationBuilder.CreateTable(
                name: "photographer_tags",
                columns: table => new
                {
                    PhotographerId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Provenance = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photographer_tags", x => new { x.PhotographerId, x.NormalizedName });
                    table.ForeignKey(
                        name: "FK_photographer_tags_photographers_PhotographerId_OwnerId",
                        columns: x => new { x.PhotographerId, x.OwnerId },
                        principalTable: "photographers",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_photographer_tags_OwnerId_NormalizedName",
                table: "photographer_tags",
                columns: new[] { "OwnerId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_photographer_tags_PhotographerId_OwnerId",
                table: "photographer_tags",
                columns: new[] { "PhotographerId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_photographers_OwnerId_CreatedAt_Id",
                table: "photographers",
                columns: new[] { "OwnerId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_photographers_OwnerId_PortfolioHash",
                table: "photographers",
                columns: new[] { "OwnerId", "PortfolioHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photographer_tags");

            migrationBuilder.DropTable(
                name: "photographers");
        }
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Loupe.Domain.Boards.Board", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("NormalizedName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("Revision")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("OwnerId", "NormalizedName")
                        .IsUnique();

                    b.ToTable("boards", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Boards.BoardReference", b =>
                {
                    b.Property<Guid>("BoardId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("ReferenceId")
                        .HasColumnType("uuid");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("BoardId", "ReferenceId");

                    b.HasIndex("BoardId", "OwnerId");

                    b.HasIndex("ReferenceId", "OwnerId");

                    b.ToTable("board_references", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Deletions.DeletionOperation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("DeletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset?>("LastAttemptAt")
                        .HasColumnType("timestamp with time zone");

                    b.PrimitiveCollection<string[]>("MediaKeys")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<Guid>("ResourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("ResourceType")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.HasKey("Id");

                    b.HasIndex("CompletedAt", "LastAttemptAt", "DeletedAt");

                    b.HasIndex("OwnerId", "ResourceType", "ResourceId")
                        .IsUnique();

                    b.ToTable("deletions", "journal");
                });

            modelBuilder.Entity("Loupe.Domain.Operations.AnalysisDispatchCursor", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.HasKey("Id");

                    b.ToTable("analysis_dispatch_cursor", (string?)null);

                    b.HasData(
                        new
                        {
                            Id = 1,
                            OwnerId = ""
                        });
                });

            modelBuilder.Entity("Loupe.Domain.Operations.BackgroundOperation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("AttemptCount")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("FailureCode")
                        .HasColumnType("text");

                    b.Property<string>("InputJson")
                        .HasColumnType("jsonb");

                    b.Property<int>("InvalidOutputCount")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset?>("LeaseExpiresAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("LeaseToken")
                        .HasColumnType("uuid");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Mode")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<string>("Model")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("NextAttemptAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("OutputJson")
                        .HasColumnType("jsonb");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("PromptVersion")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("RecoveryCount")
                        .HasColumnType("integer");

                    b.Property<Guid>("ResourceId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("RetryAvailableAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("Status", "CreatedAt");

                    b.HasIndex("OwnerId", "Type", "ResourceId");

                    b.ToTable("background_operations", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Operations.OperationReceipt", b =>
                {
                    b.Property<string>("OwnerId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("OperationType")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Key")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PayloadHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("ResourceId")
                        .HasColumnType("uuid");

                    b.HasKey("OwnerId", "OperationType", "Key");

                    b.HasIndex("CreatedAt");

                    b.ToTable("operation_receipts", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Photographers.Photographer", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Notes")
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PortfolioHash")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)")
                        .HasComputedColumnSql("md5(loupe_normalize_source(\"PortfolioUrl\"))", true);

                    b.Property<string>("PortfolioUrl")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("Revision")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(1L);

                    b.Property<string>("Summary")
                        .HasColumnType("text");

                    b.Property<string>("SummaryProvenance")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("OwnerId", "PortfolioHash");

                    b.HasIndex("OwnerId", "CreatedAt", "Id");

                    b.ToTable("photographers", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Photographers.PhotographerTag", b =>
                {
                    b.Property<Guid>("PhotographerId")
                        .HasColumnType("uuid");

                    b.Property<string>("NormalizedName")
                        .HasColumnType("text");

                    b.Property<string>("Category")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Provenance")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("PhotographerId", "NormalizedName");

                    b.HasIndex("OwnerId", "NormalizedName");

                    b.HasIndex("PhotographerId", "OwnerId");

                    b.ToTable("photographer_tags", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Photographs.Photograph", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ArchivedDemoCritiqueJson")
                        .HasColumnType("jsonb");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("CritiqueJson")
                        .HasColumnType("jsonb");

                    b.Property<Guid?>("CurrentCritiqueOperationId")
                        .HasColumnType("uuid");

                    b.Property<int>("Height")
                        .HasColumnType("integer");

                    b.Property<string>("ImageKey")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Notes")
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PreviewKey")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("Revision")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(1L);

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Width")
                        .HasColumnType("integer");

                    b.ComplexProperty(typeof(Dictionary<string, object>), "Brief", "Loupe.Domain.Photographs.Photograph.Brief#CritiqueBrief", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<int?>("Experience");

                            b1.Property<string>("Genre");

                            b1.Property<string>("Intent");

                            b1.Property<string>("RequestedFeedback");

                            b1
                                .ToJson("Brief")
                                .HasColumnType("jsonb");
                        });

                    b.ComplexProperty(typeof(Dictionary<string, object>), "Exif", "Loupe.Domain.Photographs.Photograph.Exif#CaptureMetadata", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<string>("Aperture");

                            b1.Property<string>("Camera");

                            b1.Property<string>("CapturedAt");

                            b1.Property<string>("FocalLength");

                            b1.Property<string>("Iso");

                            b1.Property<string>("Lens");

                            b1.Property<string>("ShutterSpeed");

                            b1
                                .ToJson("Exif")
                                .HasColumnType("jsonb");
                        });

                    b.HasKey("Id");

                    b.HasIndex("OwnerId", "CreatedAt", "Id");

                    b.ToTable("photographs", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.References.Reference", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Attribution")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("CurrentAnalysisOperationId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("CurrentImportOperationId")
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<string>("DescriptionProvenance")
                        .HasColumnType("text");

                    b.Property<int?>("Height")
                        .HasColumnType("integer");

                    b.Property<string>("ImageKey")
                        .HasColumnType("text");

                    b.Property<long>("ImageRevision")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(1L);

                    b.Property<string>("Notes")
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PreviewKey")
                        .HasColumnType("text");

                    b.Property<long>("Revision")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(1L);

                    b.Property<string>("SourceHash")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)")
                        .HasComputedColumnSql("md5(loupe_normalize_source(\"SourceUrl\"))", true);

                    b.Property<string>("SourceImportJson")
                        .HasColumnType("jsonb");

                    b.Property<string>("SourceUrl")
                        .HasColumnType("text");

                    b.Property<string>("SuggestionUndoJson")
                        .HasColumnType("jsonb");

                    b.Property<string>("SuggestionsJson")
                        .HasColumnType("jsonb");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int?>("Width")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("OwnerId", "SourceHash");

                    b.HasIndex("OwnerId", "CreatedAt", "Id");

                    b.ToTable("references", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.References.ReferenceDraft", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Attribution")
                        .HasColumnType("text");

                    b.Property<Guid?>("CommittedReferenceId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("FailureCode")
                        .HasColumnType("text");

                    b.Property<int?>("Height")
                        .HasColumnType("integer");

                    b.Property<string>("ImageKey")
                        .HasColumnType("text");

                    b.Property<Guid?>("ImportOperationId")
                        .HasColumnType("uuid");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PreviewKey")
                        .HasColumnType("text");

                    b.Property<long>("Revision")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint");

                    b.Property<string>("SourceImportJson")
                        .HasColumnType("jsonb");

                    b.Property<string>("SourceUrl")
                        .HasColumnType("text");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int?>("Width")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("ImportOperationId");

                    b.HasIndex("OwnerId", "ExpiresAt");

                    b.ToTable("reference_drafts", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.References.ReferenceTag", b =>
                {
                    b.Property<Guid>("ReferenceId")
                        .HasColumnType("uuid");

                    b.Property<string>("NormalizedName")
                        .HasColumnType("text");

                    b.Property<string>("Category")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Provenance")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("ReferenceId", "NormalizedName");

                    b.HasIndex("OwnerId", "NormalizedName");

                    b.HasIndex("ReferenceId", "OwnerId");

                    b.ToTable("reference_tags", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Sessions.ApplicationSession", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Issuer")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("LastSeenAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("sessions", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Boards.BoardReference", b =>
                {
                    b.HasOne("Loupe.Domain.Boards.Board", null)
                        .WithMany("References")
                        .HasForeignKey("BoardId", "OwnerId")
                        .HasPrincipalKey("Id", "OwnerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Loupe.Domain.References.Reference", null)
                        .WithMany("Boards")
                        .HasForeignKey("ReferenceId", "OwnerId")
                        .HasPrincipalKey("Id", "OwnerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Loupe.Domain.Photographers.PhotographerTag", b =>
                {
                    b.HasOne("Loupe.Domain.Photographers.Photographer", null)
                        .WithMany("Tags")
                        .HasForeignKey("PhotographerId", "OwnerId")
                        .HasPrincipalKey("Id", "OwnerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Loupe.Domain.References.ReferenceDraft", b =>
                {
                    b.HasOne("Loupe.Domain.Operations.BackgroundOperation", "ImportOperation")
                        .WithMany()
                        .HasForeignKey("ImportOperationId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("ImportOperation");
                });

            modelBuilder.Entity("Loupe.Domain.References.ReferenceTag", b =>
                {
                    b.HasOne("Loupe.Domain.References.Reference", null)
                        .WithMany("Tags")
                        .HasForeignKey("ReferenceId", "OwnerId")
                        .HasPrincipalKey("Id", "OwnerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Loupe.Domain.Boards.Board", b =>
                {
                    b.Navigation("References");
                });

            modelBuilder.Entity("Loupe.Domain.Photographers.Photographer", b =>
                {
                    b.Navigation("Tags");
                });

            modelBuilder.Entity("Loupe.Domain.References.Reference", b =>
                {
                    b.Navigation("Boards");

                    b.Navigation("Tags");
                });
        }
    }
}
